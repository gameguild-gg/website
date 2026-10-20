// SPDX-License-Identifier: UNLICENSED
pragma solidity 0.8.37;

/// @notice Digest-only evidence for one immutable issuer on a configured EVM network.
/// @dev A successful call does not establish off-chain certificate validity or chain finality.
contract CertificateAnchorRegistry {
    struct Anchor {
        address anchorSigner;
        address revokeSigner;
        uint256 anchoredAtBlock;
        uint256 revokedAtBlock;
    }

    error InvalidIssuer();
    error InvalidAddress();
    error InvalidDigest();
    error UnauthorizedOwner();
    error UnauthorizedPendingOwner();
    error UnauthorizedSigner();
    error UnknownCertificate(bytes32 certificateHash);

    event SignerAuthorizationChanged(address indexed signer, bool authorized);
    event OwnershipTransferStarted(address indexed previousOwner, address indexed pendingOwner);
    event OwnershipTransferred(address indexed previousOwner, address indexed newOwner);
    event CertificateAnchored(bytes32 indexed certificateHash, bytes32 indexed issuerId, address indexed signer, uint256 blockNumber);
    event CertificateRevoked(bytes32 indexed certificateHash, bytes32 indexed issuerId, address indexed signer, uint256 blockNumber);

    uint256 public constant PROTOCOL_VERSION = 1;
    bytes32 public immutable issuerId;
    address public owner;
    address public pendingOwner;
    mapping(address => bool) public authorizedSigners;
    mapping(bytes32 => Anchor) private anchors;

    constructor(bytes32 issuerId_, address initialOwner, address initialSigner) {
        if (issuerId_ == bytes32(0)) revert InvalidIssuer();
        if (initialOwner == address(0) || initialSigner == address(0)) revert InvalidAddress();
        issuerId = issuerId_;
        owner = initialOwner;
        authorizedSigners[initialSigner] = true;
        emit OwnershipTransferred(address(0), initialOwner);
        emit SignerAuthorizationChanged(initialSigner, true);
    }

    modifier onlyOwner() {
        if (msg.sender != owner) revert UnauthorizedOwner();
        _;
    }

    modifier onlySigner() {
        if (!authorizedSigners[msg.sender]) revert UnauthorizedSigner();
        _;
    }

    function setSigner(address signer, bool authorized) external onlyOwner {
        if (signer == address(0)) revert InvalidAddress();
        if (authorizedSigners[signer] == authorized) return;
        authorizedSigners[signer] = authorized;
        emit SignerAuthorizationChanged(signer, authorized);
    }

    /// @notice The existing owner retains authority until the proposed owner accepts.
    function beginOwnershipTransfer(address newOwner) external onlyOwner {
        if (newOwner == address(0)) revert InvalidAddress();
        pendingOwner = newOwner;
        emit OwnershipTransferStarted(owner, newOwner);
    }

    function acceptOwnership() external {
        if (msg.sender != pendingOwner) revert UnauthorizedPendingOwner();
        address previousOwner = owner;
        owner = msg.sender;
        pendingOwner = address(0);
        emit OwnershipTransferred(previousOwner, msg.sender);
    }

    /// @return changed True only for first publication; duplicates never rewrite or revive evidence.
    function anchor(bytes32 certificateHash) external onlySigner returns (bool changed) {
        if (certificateHash == bytes32(0)) revert InvalidDigest();
        Anchor storage item = anchors[certificateHash];
        if (item.anchorSigner != address(0)) return false;
        item.anchorSigner = msg.sender;
        item.anchoredAtBlock = block.number;
        emit CertificateAnchored(certificateHash, issuerId, msg.sender, block.number);
        return true;
    }

    /// @return changed True only for first revocation; no operation can remove a revocation.
    function revoke(bytes32 certificateHash) external onlySigner returns (bool changed) {
        if (certificateHash == bytes32(0)) revert InvalidDigest();
        Anchor storage item = anchors[certificateHash];
        if (item.anchorSigner == address(0)) revert UnknownCertificate(certificateHash);
        if (item.revokeSigner != address(0)) return false;
        item.revokeSigner = msg.sender;
        item.revokedAtBlock = block.number;
        emit CertificateRevoked(certificateHash, issuerId, msg.sender, block.number);
        return true;
    }

    /// @notice Read at an independently verified finalized block when checking a certificate.
    function getAnchor(bytes32 certificateHash) external view returns (Anchor memory) {
        return anchors[certificateHash];
    }
}
