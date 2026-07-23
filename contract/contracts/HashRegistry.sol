// SPDX-License-Identifier: MIT
pragma solidity ^0.8.28;

contract HashRegistry {
    struct BatchAnchor {
        bytes32 merkleRoot;
        address submitter;
        uint64 anchoredAt;
    }

    mapping(uint256 batchId => BatchAnchor anchor) public batchAnchors;

    event BatchAnchored(
        uint256 indexed batchId,
        bytes32 indexed merkleRoot,
        address indexed submitter,
        uint64 anchoredAt
    );

    function anchorBatch(uint256 batchId, bytes32 merkleRoot) external {
        require(batchId > 0, "batch id is zero");
        require(merkleRoot != bytes32(0), "root is zero");
        require(batchAnchors[batchId].anchoredAt == 0, "batch already anchored");

        uint64 anchoredAt = uint64(block.timestamp);

        batchAnchors[batchId] = BatchAnchor({
            merkleRoot: merkleRoot,
            submitter: msg.sender,
            anchoredAt: anchoredAt
        });

        emit BatchAnchored(
            batchId,
            merkleRoot,
            msg.sender,
            anchoredAt
        );
    }
}
