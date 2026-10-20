// SPDX-License-Identifier: UNLICENSED
pragma solidity 0.8.37;

import {CertificateAnchorRegistry} from "../src/CertificateAnchorRegistry.sol";

// Only the documented Foundry test boundary; no forge-std runtime dependency.
interface TestVm {
    struct Log {
        bytes32[] topics;
        bytes data;
        address emitter;
    }

    function expectRevert(bytes calldata data) external;
    function recordLogs() external;
    function getRecordedLogs() external view returns (Log[] memory);
    function roll(uint256 height) external;
}

contract RegistryActor {
    function invoke(address target, bytes calldata data) external returns (bool, bytes memory) {
        return target.call(data);
    }
}

contract CertificateAnchorRegistryTest {
    TestVm private constant VM = TestVm(address(uint160(uint256(keccak256("hevm cheat code")))));
    bytes32 private constant ISSUER = bytes32(uint256(9));
    bytes32 private constant DIGEST = bytes32(uint256(123));

    CertificateAnchorRegistry private registry;
    RegistryActor private signer;
    RegistryActor private nextSigner;
    RegistryActor private outsider;

    function setUp() public {
        signer = new RegistryActor();
        nextSigner = new RegistryActor();
        outsider = new RegistryActor();
        registry = new CertificateAnchorRegistry(ISSUER, address(this), address(signer));
    }

    function testRejectsEmptyIssuer() public {
        VM.expectRevert(abi.encodeWithSelector(CertificateAnchorRegistry.InvalidIssuer.selector));
        new CertificateAnchorRegistry(bytes32(0), address(this), address(signer));
    }

    function testRejectsEmptyOwner() public {
        VM.expectRevert(abi.encodeWithSelector(CertificateAnchorRegistry.InvalidAddress.selector));
        new CertificateAnchorRegistry(ISSUER, address(0), address(signer));
    }

    function testRejectsEmptyInitialSigner() public {
        VM.expectRevert(abi.encodeWithSelector(CertificateAnchorRegistry.InvalidAddress.selector));
        new CertificateAnchorRegistry(ISSUER, address(this), address(0));
    }

    function testInitialRolesAndUnknownDigest() public view {
        require(registry.issuerId() == ISSUER, "issuer");
        require(registry.owner() == address(this), "owner");
        require(registry.pendingOwner() == address(0), "pending owner");
        require(registry.authorizedSigners(address(signer)), "initial signer");
        require(!registry.authorizedSigners(address(this)), "admin is not implicitly signer");
        CertificateAnchorRegistry.Anchor memory item = registry.getAnchor(DIGEST);
        require(item.anchorSigner == address(0) && item.revokeSigner == address(0), "unknown digest");
    }

    function testUnapprovedAccountCannotAnchor() public {
        _expectError(outsider, abi.encodeCall(registry.anchor, (DIGEST)),
            abi.encodeWithSelector(CertificateAnchorRegistry.UnauthorizedSigner.selector));
    }

    function testAdministratorCannotAnchorWithoutSignerGrant() public {
        VM.expectRevert(abi.encodeWithSelector(CertificateAnchorRegistry.UnauthorizedSigner.selector));
        registry.anchor(DIGEST);
    }

    function testSignerCannotManageSignersOrOwnership() public {
        _expectError(signer, abi.encodeCall(registry.setSigner, (address(outsider), true)),
            abi.encodeWithSelector(CertificateAnchorRegistry.UnauthorizedOwner.selector));
        _expectError(signer, abi.encodeCall(registry.beginOwnershipTransfer, (address(outsider))),
            abi.encodeWithSelector(CertificateAnchorRegistry.UnauthorizedOwner.selector));
        require(!registry.authorizedSigners(address(outsider)), "unauthorized grant persisted");
    }

    function testRejectsEmptyDigestForAnchorAndRevoke() public {
        bytes memory expected = abi.encodeWithSelector(CertificateAnchorRegistry.InvalidDigest.selector);
        _expectError(signer, abi.encodeCall(registry.anchor, (bytes32(0))), expected);
        _expectError(signer, abi.encodeCall(registry.revoke, (bytes32(0))), expected);
    }

    function testAnchorRecordsIssuerSignerAndBlockEvent() public {
        VM.roll(150);
        VM.recordLogs();
        require(abi.decode(_invoke(signer, abi.encodeCall(registry.anchor, (DIGEST))), (bool)), "first anchor");
        CertificateAnchorRegistry.Anchor memory item = registry.getAnchor(DIGEST);
        require(item.anchorSigner == address(signer), "anchor signer");
        require(item.anchoredAtBlock == 150, "anchor block");
        require(item.revokeSigner == address(0) && item.revokedAtBlock == 0, "not revoked");
        _assertEvent(VM.getRecordedLogs(), "CertificateAnchored(bytes32,bytes32,address,uint256)", address(signer), 150);
    }

    function testRepeatedAnchorIsIdempotentAndDoesNotRewriteEvidence() public {
        _invoke(signer, abi.encodeCall(registry.anchor, (DIGEST)));
        registry.setSigner(address(nextSigner), true);
        CertificateAnchorRegistry.Anchor memory beforeItem = registry.getAnchor(DIGEST);
        VM.roll(250);
        VM.recordLogs();
        require(!abi.decode(_invoke(nextSigner, abi.encodeCall(registry.anchor, (DIGEST))), (bool)), "duplicate changed state");
        CertificateAnchorRegistry.Anchor memory afterItem = registry.getAnchor(DIGEST);
        require(afterItem.anchorSigner == beforeItem.anchorSigner, "original signer overwritten");
        require(afterItem.anchoredAtBlock == beforeItem.anchoredAtBlock, "original block overwritten");
        require(VM.getRecordedLogs().length == 0, "duplicate event");
    }

    function testUnapprovedAccountCannotRevoke() public {
        _invoke(signer, abi.encodeCall(registry.anchor, (DIGEST)));
        _expectError(outsider, abi.encodeCall(registry.revoke, (DIGEST)),
            abi.encodeWithSelector(CertificateAnchorRegistry.UnauthorizedSigner.selector));
        require(registry.getAnchor(DIGEST).revokeSigner == address(0), "unauthorized revocation persisted");
    }

    function testCannotRevokeUnknownDigest() public {
        _expectError(signer, abi.encodeCall(registry.revoke, (DIGEST)),
            abi.encodeWithSelector(CertificateAnchorRegistry.UnknownCertificate.selector, DIGEST));
    }

    function testRotationPreservesOriginalSignerAndAllowsRevocation() public {
        VM.roll(150);
        _invoke(signer, abi.encodeCall(registry.anchor, (DIGEST)));
        registry.setSigner(address(signer), false);
        registry.setSigner(address(nextSigner), true);
        _expectError(signer, abi.encodeCall(registry.revoke, (DIGEST)),
            abi.encodeWithSelector(CertificateAnchorRegistry.UnauthorizedSigner.selector));
        VM.roll(200);
        VM.recordLogs();
        require(abi.decode(_invoke(nextSigner, abi.encodeCall(registry.revoke, (DIGEST))), (bool)), "first revoke");
        CertificateAnchorRegistry.Anchor memory item = registry.getAnchor(DIGEST);
        require(item.anchorSigner == address(signer) && item.anchoredAtBlock == 150, "original evidence");
        require(item.revokeSigner == address(nextSigner) && item.revokedAtBlock == 200, "revocation evidence");
        _assertEvent(VM.getRecordedLogs(), "CertificateRevoked(bytes32,bytes32,address,uint256)", address(nextSigner), 200);
    }

    function testRepeatedRevokeIsIdempotentAndCannotBeUndoneByAnchor() public {
        _invoke(signer, abi.encodeCall(registry.anchor, (DIGEST)));
        _invoke(signer, abi.encodeCall(registry.revoke, (DIGEST)));
        CertificateAnchorRegistry.Anchor memory beforeItem = registry.getAnchor(DIGEST);
        registry.setSigner(address(nextSigner), true);
        VM.roll(300);
        VM.recordLogs();
        require(!abi.decode(_invoke(nextSigner, abi.encodeCall(registry.revoke, (DIGEST))), (bool)), "duplicate revoke");
        require(!abi.decode(_invoke(nextSigner, abi.encodeCall(registry.anchor, (DIGEST))), (bool)), "reanchor revived");
        CertificateAnchorRegistry.Anchor memory item = registry.getAnchor(DIGEST);
        require(item.anchorSigner == beforeItem.anchorSigner && item.anchoredAtBlock == beforeItem.anchoredAtBlock, "anchor changed");
        require(item.revokeSigner == beforeItem.revokeSigner && item.revokedAtBlock == beforeItem.revokedAtBlock, "revocation changed");
        require(VM.getRecordedLogs().length == 0, "duplicate event");
    }

    function testSignerRemovalPreventsFurtherWrites() public {
        registry.setSigner(address(signer), false);
        _expectError(signer, abi.encodeCall(registry.anchor, (DIGEST)),
            abi.encodeWithSelector(CertificateAnchorRegistry.UnauthorizedSigner.selector));
        require(registry.getAnchor(DIGEST).anchorSigner == address(0), "removed signer wrote");
    }

    function testRejectsEmptySignerAndOwnershipTarget() public {
        VM.expectRevert(abi.encodeWithSelector(CertificateAnchorRegistry.InvalidAddress.selector));
        registry.setSigner(address(0), true);
        VM.expectRevert(abi.encodeWithSelector(CertificateAnchorRegistry.InvalidAddress.selector));
        registry.beginOwnershipTransfer(address(0));
        require(registry.pendingOwner() == address(0), "invalid pending owner");
    }

    function testOwnershipRequiresAcceptanceAndDoesNotGrantSigning() public {
        registry.beginOwnershipTransfer(address(outsider));
        require(registry.owner() == address(this), "transferred without acceptance");
        _expectError(nextSigner, abi.encodeCall(registry.acceptOwnership, ()),
            abi.encodeWithSelector(CertificateAnchorRegistry.UnauthorizedPendingOwner.selector));
        _invoke(outsider, abi.encodeCall(registry.acceptOwnership, ()));
        require(registry.owner() == address(outsider) && registry.pendingOwner() == address(0), "accepted owner");
        VM.expectRevert(abi.encodeWithSelector(CertificateAnchorRegistry.UnauthorizedOwner.selector));
        registry.setSigner(address(nextSigner), true);
        _expectError(outsider, abi.encodeCall(registry.anchor, (DIGEST)),
            abi.encodeWithSelector(CertificateAnchorRegistry.UnauthorizedSigner.selector));
        _invoke(outsider, abi.encodeCall(registry.setSigner, (address(nextSigner), true)));
        require(registry.authorizedSigners(address(nextSigner)), "new owner grant");
        _invoke(signer, abi.encodeCall(registry.anchor, (DIGEST)));
        require(registry.getAnchor(DIGEST).anchorSigner == address(signer), "transfer lost signer");
    }

    function testSupersededPendingOwnerCannotAccept() public {
        registry.beginOwnershipTransfer(address(outsider));
        registry.beginOwnershipTransfer(address(nextSigner));
        _expectError(outsider, abi.encodeCall(registry.acceptOwnership, ()),
            abi.encodeWithSelector(CertificateAnchorRegistry.UnauthorizedPendingOwner.selector));
        _invoke(nextSigner, abi.encodeCall(registry.acceptOwnership, ()));
        require(registry.owner() == address(nextSigner), "replacement owner");
    }

    function testFuzzDigestIsolation(bytes32 first, bytes32 second) public {
        if (first == bytes32(0) || second == bytes32(0) || first == second) return;
        _invoke(signer, abi.encodeCall(registry.anchor, (first)));
        require(registry.getAnchor(second).anchorSigner == address(0), "digest collision");
        _invoke(signer, abi.encodeCall(registry.anchor, (second)));
        _invoke(signer, abi.encodeCall(registry.revoke, (first)));
        require(registry.getAnchor(first).revokeSigner == address(signer), "first not revoked");
        require(registry.getAnchor(second).revokeSigner == address(0), "revoked other digest");
    }

    function _invoke(RegistryActor actor, bytes memory payload) private returns (bytes memory data) {
        bool ok;
        (ok, data) = actor.invoke(address(registry), payload);
        require(ok, "expected successful actor operation");
    }

    function _expectError(RegistryActor actor, bytes memory payload, bytes memory expected) private {
        (bool ok, bytes memory data) = actor.invoke(address(registry), payload);
        require(!ok && keccak256(data) == keccak256(expected), "unexpected revert result");
    }

    function _assertEvent(TestVm.Log[] memory logs, string memory signature, address actor, uint256 height) private view {
        require(logs.length == 1 && logs[0].emitter == address(registry), "event emitter/count");
        require(logs[0].topics.length == 4 && logs[0].topics[0] == keccak256(bytes(signature)), "event signature");
        require(logs[0].topics[1] == DIGEST && logs[0].topics[2] == ISSUER, "event digest/issuer");
        require(logs[0].topics[3] == bytes32(uint256(uint160(actor))), "event actor");
        require(abi.decode(logs[0].data, (uint256)) == height, "event block");
    }
}
