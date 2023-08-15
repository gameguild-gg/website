import React, { useState } from 'react';
import { ethers } from 'ethers';

const LoginPage: React.FC = () => {
    const [provider, setProvider] = useState<ethers.providers.Web3Provider | null>(null);
    const [account, setAccount] = useState<string | null>(null);

    const connectToMetamask = async () => {
        try {
            if (window.ethereum) {
                const ethereum = window.ethereum;
                await ethereum.request({ method: 'eth_requestAccounts' });

                const web3Provider = new ethers.providers.Web3Provider(ethereum);
                const signer = web3Provider.getSigner();
                const currentAccount = await signer.getAddress();

                setProvider(web3Provider);
                setAccount(currentAccount);
            } else {
                console.error('Metamask not detected.');
            }
        } catch (error) {
            console.error('Error connecting to Metamask:', error);
        }
    };

    return (
        <div className="flex h-screen justify-center items-center">
            <div className="max-w-md w-full p-6 bg-white border rounded-lg shadow">
                <h1 className="text-2xl font-semibold mb-4">Login with Metamask</h1>
                {account ? (
                    <div>
                        <p className="mb-2">Hello, user with wallet: {account}</p>
                        <button
                            onClick={() => {
                                // Implement your login logic here
                            }}
                            className="w-full bg-green-500 text-white p-2 rounded-md"
                        >
                            Log in
                        </button>
                    </div>
                ) : (
                    <div>
                        <p>Please connect your Metamask wallet to continue.</p>
                        <button
                            onClick={connectToMetamask}
                            className="w-full bg-blue-500 text-white p-2 rounded-md mt-2"
                        >
                            Connect with Metamask
                        </button>
                    </div>
                )}
            </div>
        </div>
    );
};

export default LoginPage;
