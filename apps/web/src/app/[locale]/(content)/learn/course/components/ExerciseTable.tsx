interface Exercise {
  title: string
  description: string
  type: 'assignment' | 'example'
}

interface ExerciseTableProps {
  exercises: { [key: string]: Exercise }
  onExerciseClick: (exerciseId: string) => void
}

export default function ExerciseTable({ exercises, onExerciseClick }: ExerciseTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="min-w-full bg-white shadow-md rounded-lg overflow-hidden">
        <thead className="bg-gray-200">
          <tr>
            <th className="px-4 py-3 text-left text-sm font-semibold text-gray-600 uppercase tracking-wider">Title</th>
            <th className="px-4 py-3 text-left text-sm font-semibold text-gray-600 uppercase tracking-wider">Description</th>
            <th className="px-4 py-3 text-left text-sm font-semibold text-gray-600 uppercase tracking-wider">Type</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-200">
          {Object.entries(exercises).map(([id, exercise]) => (
            <tr key={id} className="hover:bg-gray-50">
              <td className="px-4 py-3">
                <button
                  onClick={() => onExerciseClick(id)}
                  className="text-blue-600 hover:text-blue-800 font-medium"
                >
                  {exercise.title}
                </button>
              </td>
              <td className="px-4 py-3 text-sm text-gray-500">
                {exercise.description.split('\n')[0]}
              </td>
              <td className="px-4 py-3 text-sm text-gray-500">
                {exercise.type}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

