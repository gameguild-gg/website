'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import Link from 'next/link' // Importe o componente Link

export default function Home() {
  const [userId, setUserId] = useState('')
  const [error, setError] = useState('')
  const router = useRouter()

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('') // Clear any previous errors

    if (userId) {
      try {
        const response = await fetch(`../../../api/learn/user/${userId}`)
        if (response.ok) {
          router.push(`/learn/courses?userId=${encodeURIComponent(userId)}`)
        } else {
          setError('Unable to connect to this user. Please check the ID and try again.')
        }
      } catch (error) {
        console.error('Error checking user:', error)
        setError('An error occurred. Please try again.')
      }
    }
  }

  return (
    <div className="min-h-screen flex flex-col items-center justify-center bg-gray-100">
      <form onSubmit={handleSubmit} className="bg-white p-8 rounded-lg shadow-md w-96">
        <h1 className="text-2xl font-bold mb-4">Welcome to GameGuild Learning Platform</h1>
        <div className="mb-4">
          <label htmlFor="userId" className="block mb-2">Enter your User ID:</label>
          <input
            type="text"
            id="userId"
            value={userId}
            onChange={(e) => setUserId(e.target.value)}
            className="w-full px-3 py-2 border rounded"
            required
          />
        </div>
        {error && (
          <div className="mb-4 text-red-500">
            {error}
          </div>
        )}
        <button
          type="submit"
          className="w-full bg-blue-500 text-white py-2 rounded hover:bg-blue-600 mb-4" // Adicione margem inferior
          disabled={!userId}
        >
          Continue
        </button>
         <Link href={`/learn/coding-environment?id=0&type=sandbox&userId=${userId}`}> {/* Adicione o link para o sandbox */}
          <button className="w-full bg-gray-300 text-gray-700 py-2 rounded hover:bg-gray-400">
            Try Sandbox
          </button>
        </Link>
      </form>
    </div>
  )
}