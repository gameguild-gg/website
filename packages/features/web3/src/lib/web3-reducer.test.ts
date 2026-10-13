import { describe, expect, it } from 'vitest';
import { Web3ActionType, defaultWeb3State, web3Reducer } from './web3-reducer';
import type { Web3Error } from '../types';

const connectError: Web3Error = { code: 'user_rejected', message: 'User rejected the request.' };

describe('web3Reducer', () => {
  it('starts disconnected with no wallet, account or chain', () => {
    expect(defaultWeb3State).toEqual({
      status: 'disconnected',
      walletId: undefined,
      walletName: undefined,
      account: undefined,
      chainId: undefined,
      error: undefined,
    });
  });

  it('tracks the wallet through connecting to connected', () => {
    let state = web3Reducer(defaultWeb3State, {
      type: Web3ActionType.CONNECT_START,
      walletId: 'metamask',
      walletName: 'MetaMask',
    });
    expect(state).toMatchObject({ status: 'connecting', walletId: 'metamask', walletName: 'MetaMask' });

    state = web3Reducer(state, {
      type: Web3ActionType.CONNECT_SUCCESS,
      account: '0xabc',
      chainId: '1',
    });
    expect(state).toMatchObject({
      status: 'connected',
      account: '0xabc',
      chainId: '1',
      error: undefined,
    });
  });

  it('represents connect failures as the error state and clears account state', () => {
    const connecting = web3Reducer(defaultWeb3State, {
      type: Web3ActionType.CONNECT_START,
      walletId: 'torus',
      walletName: 'Torus',
    });
    const failed = web3Reducer(connecting, {
      type: Web3ActionType.CONNECT_FAILURE,
      error: connectError,
    });
    expect(failed).toMatchObject({
      status: 'error',
      account: undefined,
      chainId: undefined,
      error: connectError,
    });
  });

  it('represents the signing and authenticated states', () => {
    let state = web3Reducer(defaultWeb3State, {
      type: Web3ActionType.CONNECT_SUCCESS,
      account: '0xabc',
      chainId: '1',
    });
    state = web3Reducer(state, { type: Web3ActionType.AUTH_SIGNING });
    expect(state.status).toBe('signing');

    state = web3Reducer(state, { type: Web3ActionType.AUTH_SUCCESS });
    expect(state.status).toBe('authenticated');
    expect(state.error).toBeUndefined();
  });

  it('returns to connected (not error) when verification fails on a live connection', () => {
    let state = web3Reducer(defaultWeb3State, {
      type: Web3ActionType.CONNECT_SUCCESS,
      account: '0xabc',
      chainId: '1',
    });
    state = web3Reducer(state, { type: Web3ActionType.AUTH_SIGNING });
    state = web3Reducer(state, {
      type: Web3ActionType.AUTH_FAILURE,
      error: { code: 'verification_failed', message: 'Rejected' },
    });
    expect(state.status).toBe('connected');
    expect(state.error).toMatchObject({ code: 'verification_failed' });
  });

  it('handles account and network changes', () => {
    let state = web3Reducer(defaultWeb3State, {
      type: Web3ActionType.CONNECT_SUCCESS,
      account: '0xabc',
      chainId: '1',
    });
    state = web3Reducer(state, { type: Web3ActionType.AUTH_SUCCESS });

    state = web3Reducer(state, { type: Web3ActionType.ACCOUNTS_CHANGED, account: '0xdef' });
    // Switching accounts invalidates the authentication.
    expect(state).toMatchObject({ status: 'connected', account: '0xdef' });

    state = web3Reducer(state, { type: Web3ActionType.CHAIN_CHANGED, chainId: '11155111' });
    expect(state).toMatchObject({ chainId: '11155111', account: '0xdef' });
  });

  it('fully resets on wallet disconnect', () => {
    let state = web3Reducer(defaultWeb3State, {
      type: Web3ActionType.CONNECT_START,
      walletId: 'metamask',
      walletName: 'MetaMask',
    });
    state = web3Reducer(state, {
      type: Web3ActionType.CONNECT_SUCCESS,
      account: '0xabc',
      chainId: '1',
    });
    state = web3Reducer(state, { type: Web3ActionType.AUTH_SUCCESS });

    const reset = web3Reducer(state, { type: Web3ActionType.WALLET_DISCONNECTED });
    expect(reset).toEqual({ ...defaultWeb3State, error: undefined });
  });

  it('carries a disconnect reason into the reset state', () => {
    const state = web3Reducer(defaultWeb3State, {
      type: Web3ActionType.CONNECT_SUCCESS,
      account: '0xabc',
      chainId: '1',
    });
    const reset = web3Reducer(state, {
      type: Web3ActionType.WALLET_DISCONNECTED,
      error: { code: 'disconnected', message: 'Extension removed.' },
    });
    expect(reset.status).toBe('disconnected');
    expect(reset.error).toEqual({ code: 'disconnected', message: 'Extension removed.' });
  });

  it('clearError keeps connected accounts connected', () => {
    let state = web3Reducer(defaultWeb3State, {
      type: Web3ActionType.CONNECT_SUCCESS,
      account: '0xabc',
      chainId: '1',
    });
    state = web3Reducer(state, {
      type: Web3ActionType.AUTH_FAILURE,
      error: { code: 'verification_failed', message: 'Rejected' },
    });
    state = web3Reducer(state, { type: Web3ActionType.CLEAR_ERROR });
    expect(state.status).toBe('connected');
    expect(state.error).toBeUndefined();
    expect(state.account).toBe('0xabc');
  });

  it('clearError returns account-less error states to disconnected', () => {
    let state = web3Reducer(defaultWeb3State, {
      type: Web3ActionType.CONNECT_FAILURE,
      error: connectError,
    });
    state = web3Reducer(state, { type: Web3ActionType.CLEAR_ERROR });
    expect(state.status).toBe('disconnected');
    expect(state.error).toBeUndefined();
  });
});
