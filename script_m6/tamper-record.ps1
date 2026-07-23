<#
.SYNOPSIS
Changes one field of one sensor_records row for the M6 tamper demo.

.DESCRIPTION
The script updates exactly one row in PostgreSQL. It does not update Besu.

Current verification protects:
  - Merkle leaf: id + original_json

So:
  - Field ph changes original_json -> Merkle root becomes different.
  - Field id changes Merkle leaf input -> Merkle root becomes different.
  - Field status can hide the Verify button if changed away from anchored.
  - Field batch_id moves the record to another batch and breaks that batch check.

.PARAMETER RecordId
The sensor_records.id value to change.

.PARAMETER Field
The field to change: id, status, batch_id, or ph.

.PARAMETER Value
Optional replacement value. When omitted, the script chooses a demo value.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File .\m6\tamper-record.ps1 -RecordId 25 -Field ph

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File .\m6\tamper-record.ps1 -RecordId 25 -Field batch_id -Value 2
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateRange(1, [long]::MaxValue)]
    [long] $RecordId,

    [Parameter(Mandatory)]
    [ValidateSet('id', 'status', 'batch_id', 'ph')]
    [string] $Field,

    [string] $Value
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $projectRoot 'backend\docker-compose.yml'

if (-not (Test-Path -LiteralPath $composeFile)) {
    throw "Backend compose file was not found at $composeFile."
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker CLI was not found.'
}

function ConvertTo-SqlLiteral {
    param(
        [AllowNull()]
        [string] $Text
    )

    if ($null -eq $Text) {
        return 'NULL'
    }

    return "'" + $Text.Replace("'", "''") + "'"
}

function Invoke-PostgresQuery {
    param(
        [Parameter(Mandatory)]
        [string] $Sql
    )

    $output = @(& docker compose `
        -f $composeFile `
        exec -T postgres `
        psql -X -q -v ON_ERROR_STOP=1 `
        -U hash_demo -d hash_demo `
        --no-align --tuples-only `
        -c $Sql)

    if ($LASTEXITCODE -ne 0) {
        throw 'PostgreSQL command failed. Make sure the backend postgres service is running.'
    }

    return $output
}

function Get-TamperSql {
    param(
        [Parameter(Mandatory)]
        [long] $TargetRecordId,

        [Parameter(Mandatory)]
        [string] $TargetField,

        [AllowNull()]
        [string] $ReplacementValue
    )

    $hasValue = $PSBoundParameters.ContainsKey('ReplacementValue') -and
        -not [string]::IsNullOrWhiteSpace($ReplacementValue)

    switch ($TargetField) {
        'id' {
            $newIdExpression = if ($hasValue) { [long]$ReplacementValue } else { '(SELECT COALESCE(MAX(id), 0) + 1000 FROM sensor_records)' }
            return @"
WITH target AS (
  SELECT id AS old_value
  FROM sensor_records
  WHERE id = $TargetRecordId
  FOR UPDATE
),
updated AS (
  UPDATE sensor_records AS record
  SET id = $newIdExpression
  FROM target
  WHERE record.id = target.old_value
  RETURNING target.old_value, record.id AS new_value
)
SELECT format('Changed sensor_records.id: %s -> %s', old_value, new_value)
FROM updated;
"@
        }
        'status' {
            $newStatus = if ($hasValue) { ConvertTo-SqlLiteral $ReplacementValue } else { "'pending'" }
            return @"
WITH updated AS (
  UPDATE sensor_records
  SET status = $newStatus
  WHERE id = $TargetRecordId
  RETURNING id, batch_id, status
)
SELECT format('Changed record %s status -> %s (batch=%s)', id, status, batch_id)
FROM updated;
"@
        }
        'batch_id' {
            $newBatchId = if ($hasValue) { [long]$ReplacementValue } else { 'replacement.id' }
            $replacementCte = if ($hasValue) {
                @"
replacement AS (
  SELECT $newBatchId::bigint AS id
),
"@
            }
            else {
                @"
replacement AS (
  SELECT batch.id
  FROM anchor_batches AS batch
  JOIN sensor_records AS target ON target.id = $TargetRecordId
  WHERE batch.status = 'anchored'
    AND batch.id <> target.batch_id
  ORDER BY batch.id DESC
  LIMIT 1
),
"@
            }

            return @"
WITH
$replacementCte
updated AS (
  UPDATE sensor_records AS record
  SET batch_id = replacement.id
  FROM replacement
  WHERE record.id = $TargetRecordId
  RETURNING record.id, record.batch_id, record.status
)
SELECT format('Changed record %s batch_id -> %s (status=%s)', id, batch_id, status)
FROM updated;
"@
        }
        'ph' {
            $newPhExpression = if ($hasValue) {
                "to_jsonb(($([decimal]$ReplacementValue))::numeric)"
            }
            else {
                "to_jsonb(((original_json::jsonb #>> '{readings,ph}')::numeric + 1)::numeric)"
            }

            return @"
WITH updated AS (
  UPDATE sensor_records
  SET original_json = jsonb_set(
      original_json::jsonb,
      '{readings,ph}',
      $newPhExpression,
      false
    )::text
  WHERE id = $TargetRecordId
  RETURNING
    id,
    batch_id,
    status,
    original_json::jsonb #>> '{readings,ph}' AS new_value
)
SELECT format('Changed record %s original_json.readings.ph -> %s (batch=%s, status=%s)', id, new_value, batch_id, status)
FROM updated;
"@
        }
        default {
            throw "Unsupported field: $TargetField."
        }
    }
}

$findRecordSql = @"
SELECT format(
  'Target record id=%s, batch=%s, status=%s',
  id,
  batch_id,
  status
)
FROM sensor_records
WHERE id = $RecordId;
"@

$target = Invoke-PostgresQuery -Sql $findRecordSql |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -Last 1

if ([string]::IsNullOrWhiteSpace($target)) {
    throw "Record $RecordId was not found."
}

Write-Host $target

$tamperSql = Get-TamperSql -TargetRecordId $RecordId -TargetField $Field -ReplacementValue $Value
$result = Invoke-PostgresQuery -Sql $tamperSql |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -Last 1

if ([string]::IsNullOrWhiteSpace($result)) {
    if ($Field -eq 'batch_id' -and [string]::IsNullOrWhiteSpace($Value)) {
        throw 'Could not change batch_id automatically. Create at least two anchored batches or pass -Value with an existing anchor_batches.id.'
    }

    throw "Record $RecordId could not be changed."
}

Write-Host $result
Write-Host 'Open the dashboard and click Verify on the affected anchored record/batch.'
