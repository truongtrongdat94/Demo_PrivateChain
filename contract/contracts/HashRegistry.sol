// SPDX-License-Identifier: MIT
pragma solidity ^0.8.28;

contract HashRegistry {
    struct Anchor {
        uint256 sequence;
        bytes32 payloadHash;
        address submitter;
        uint64 anchoredAt;
    }

    uint256 public lastSequence;
    mapping(uint256 sequence => Anchor anchor) private anchors;

    event HashAnchored(
        uint256 indexed sequence,
        bytes32 indexed payloadHash,
        address indexed submitter,
        uint64 anchoredAt
    );

    function anchorHash(bytes32 payloadHash) external {
        require(payloadHash != bytes32(0), "hash is zero");

        uint256 sequence = lastSequence + 1;
        uint64 anchoredAt = uint64(block.timestamp);

        lastSequence = sequence;
        anchors[sequence] = Anchor({
            sequence: sequence,
            payloadHash: payloadHash,
            submitter: msg.sender,
            anchoredAt: anchoredAt
        });

        emit HashAnchored(
            sequence,
            payloadHash,
            msg.sender,
            anchoredAt
        );
    }

    function getAnchor(uint256 sequence) external view returns (Anchor memory) {
        require(sequence > 0 && sequence <= lastSequence, "anchor not found");
        return anchors[sequence];
    }
}
