// SPDX-License-Identifier: MIT
pragma solidity ^0.8.28;

contract HashRegistry {
    struct Anchor {
        uint256 sequence;
        bytes32 previousHash;
        address submitter;
        uint64 anchoredAt;
        bool exists;
    }

    mapping(bytes32 payloadHash => Anchor anchor) private anchors;
    uint256 public lastSequence;
    bytes32 public lastPayloadHash;

    event HashAnchored(
        uint256 indexed sequence,
        bytes32 indexed payloadHash,
        bytes32 previousHash,
        address indexed submitter,
        uint64 anchoredAt
    );

    function anchorHash(
        uint256 sequence,
        bytes32 payloadHash,
        bytes32 previousHash
    ) external {
        require(payloadHash != bytes32(0), "hash is zero");
        require(!anchors[payloadHash].exists, "hash already anchored");
        require(sequence == lastSequence + 1, "invalid sequence");
        require(previousHash == lastPayloadHash, "invalid previous hash");

        anchors[payloadHash] = Anchor({
            sequence: sequence,
            previousHash: previousHash,
            submitter: msg.sender,
            anchoredAt: uint64(block.timestamp),
            exists: true
        });
        lastSequence = sequence;
        lastPayloadHash = payloadHash;

        emit HashAnchored(
            sequence,
            payloadHash,
            previousHash,
            msg.sender,
            uint64(block.timestamp)
        );
    }
}
