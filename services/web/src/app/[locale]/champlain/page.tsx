'use client';
import React, { useState } from 'react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  PolarAngleAxis,
  PolarGrid,
  PolarRadiusAxis,
  Radar,
  RadarChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

// Main PLOs data
const PLOs = [
  {
    id: 'PLO1',
    title: 'Reflective Learning & Design',
    desc: 'Students will engage in reflective learning and exploration, applying human-centered design principles to tackle real-world challenges in workplace and civic life.',
  },
  {
    id: 'PLO2',
    title: 'Creative Problem Solving',
    desc: 'Students will solve problems creatively, working within defined parameters to develop innovative solutions that are practical and actionable.',
  },
  {
    id: 'PLO3',
    title: 'Effective Communication',
    desc: 'Students will communicate clearly and effectively with diverse audiences, including peers, clients, and stakeholders, in various professional settings.',
  },
  {
    id: 'PLO4',
    title: 'Adaptability & Resilience',
    desc: 'Students will adapt to changing professional contexts, demonstrating resilience and flexibility in managing evolving project requirements and challenges.',
  },
  {
    id: 'PLO5',
    title: 'Interdisciplinary Collaboration',
    desc: 'Students will collaborate effectively within interdisciplinary teams, defining roles and responsibilities to achieve common project goals.',
  },
  {
    id: 'PLO6',
    title: 'Contextual Awareness & Ethics',
    desc: 'Students will apply their awareness of self and of the world on a local, regional, or global scale to foster sustainable and ethical decision-making.',
  },
  {
    id: 'PLO7',
    title: 'Inclusive Practices',
    desc: 'Students will engage in inclusive practices that promote respect, understanding, and equity in professional and collaborative environments.',
  },
];

// Course sequence data
const courseSequence = [
  {
    id: 'PLS-180',
    name: 'PLS-180',
    fullName: 'PLS-180: Understand',
    desc: 'Foundational course focused on understanding basic concepts',
  },
  {
    id: 'PLS-280',
    name: 'PLS-280',
    fullName: 'PLS-280: Explore',
    desc: 'Intermediate course focused on exploration and application',
  },
  {
    id: 'PLS-380',
    name: 'PLS-380',
    fullName: 'PLS-380: Materialize',
    desc: 'Advanced course focused on implementation and materialization',
  },
];

// Alignment strength data (simplified for visualization)
const alignmentData = [
  // Format: [PLO id, Course id, strength (0-1), faculty emphasis]
  // Faculty emphasis levels: M&C, EJZ, WLD, JP in order
  { plo: 'PLO1', course: 'PLS-180', strength: 0.85, faculty: [0.9, 0.95, 0.8, 0.9] },
  { plo: 'PLO1', course: 'PLS-280', strength: 0.9, faculty: [0.0, 0.95, 0.85, 0.95] },
  { plo: 'PLO1', course: 'PLS-380', strength: 0.95, faculty: [0.0, 0.95, 0.9, 0.95] },

  { plo: 'PLO2', course: 'PLS-180', strength: 0.4, faculty: [0.0, 0.9, 0.0, 0.0] },
  { plo: 'PLO2', course: 'PLS-280', strength: 0.85, faculty: [0.9, 0.95, 0.8, 0.9] },
  { plo: 'PLO2', course: 'PLS-380', strength: 0.95, faculty: [0.9, 0.95, 0.7, 0.95] },

  { plo: 'PLO3', course: 'PLS-180', strength: 0.8, faculty: [0.9, 0.9, 0.0, 0.9] },
  { plo: 'PLO3', course: 'PLS-280', strength: 0.85, faculty: [0.0, 0.9, 0.0, 0.9] },
  { plo: 'PLO3', course: 'PLS-380', strength: 0.95, faculty: [0.9, 0.95, 0.0, 0.95] },

  { plo: 'PLO4', course: 'PLS-180', strength: 0.75, faculty: [0.9, 0.9, 0.7, 0.9] },
  { plo: 'PLO4', course: 'PLS-280', strength: 0.85, faculty: [0.9, 0.9, 0.0, 0.9] },
  { plo: 'PLO4', course: 'PLS-380', strength: 0.9, faculty: [0.0, 0.95, 0.0, 0.95] },

  { plo: 'PLO5', course: 'PLS-180', strength: 0.6, faculty: [0.0, 0.9, 0.8, 0.9] },
  { plo: 'PLO5', course: 'PLS-280', strength: 0.85, faculty: [0.9, 0.9, 0.8, 0.9] },
  { plo: 'PLO5', course: 'PLS-380', strength: 0.95, faculty: [0.9, 0.95, 0.8, 0.95] },

  { plo: 'PLO6', course: 'PLS-180', strength: 0.7, faculty: [0.9, 0.9, 0.8, 0.0] },
  { plo: 'PLO6', course: 'PLS-280', strength: 0.8, faculty: [0.9, 0.9, 0.8, 0.9] },
  { plo: 'PLO6', course: 'PLS-380', strength: 0.95, faculty: [0.9, 0.95, 0.9, 0.95] },

  { plo: 'PLO7', course: 'PLS-180', strength: 0.4, faculty: [0.0, 0.9, 0.8, 0.9] },
  { plo: 'PLO7', course: 'PLS-280', strength: 0.8, faculty: [0.9, 0.9, 0.8, 0.9] },
  { plo: 'PLO7', course: 'PLS-380', strength: 0.95, faculty: [0.9, 0.95, 0.9, 0.95] },
];

// Prepare radar chart data - progression of PLO emphasis across courses
const radarData = PLOs.map((plo) => {
  const result = { name: plo.id };
  courseSequence.forEach((course) => {
    const matchingData = alignmentData.find((item) => item.plo === plo.id && item.course === course.id);
    result[course.id] = matchingData ? matchingData.strength * 100 : 0;
  });
  return result;
});

// Colors for PLO groups
const PLO_COLORS = [
  '#3b82f6', // blue
  '#ef4444', // red
  '#10b981', // green
  '#f59e0b', // amber
  '#8b5cf6', // violet
  '#ec4899', // pink
  '#6366f1', // indigo
];

// Custom tooltip for the bar chart
const CustomTooltip = (props) => {
  const { active, payload } = props;
  if (active && payload && payload.length) {
    const data = payload[0].payload;
    return (
      <div className="bg-white p-4 border border-gray-200 shadow-md rounded">
        <p className="font-bold">{`${data.plo} in ${data.course}`}</p>
        <p>{`Alignment Strength: ${(data.strength * 100).toFixed(0)}%`}</p>
        <p>Faculty Emphasis:</p>
        <ul>
          <li>M&C: {(data.faculty[0] * 100).toFixed(0)}%</li>
          <li>EJZ: {(data.faculty[1] * 100).toFixed(0)}%</li>
          <li>WLD: {(data.faculty[2] * 100).toFixed(0)}%</li>
          <li>JP: {(data.faculty[3] * 100).toFixed(0)}%</li>
        </ul>
      </div>
    );
  }
  return null;
};

// Main component
const CurriculumAlignment = () => {
  const [activePLO, setActivePLO] = useState(null);
  const [activeCourse, setActiveCourse] = useState(null);
  const [activeView, setActiveView] = useState('curriculumMap');

  // Filter data based on active selections
  const filteredData = alignmentData.filter((item) => (!activePLO || item.plo === activePLO) && (!activeCourse || item.course === activeCourse));

  // Get progression data for a specific PLO across courses
  const getProgressionData = (ploId) => {
    return alignmentData
      .filter((item) => item.plo === ploId)
      .sort((a, b) => {
        const courseOrder = { 'PLS-180': 0, 'PLS-280': 1, 'PLS-380': 2 };
        return courseOrder[a.course] - courseOrder[b.course];
      });
  };

  const renderSelectedView = () => {
    switch (activeView) {
      case 'curriculumMap':
        return (
          <div>
            <div className="mb-4 p-4 bg-yellow-50 rounded border border-yellow-200">
              <p className="text-sm">
                <span className="font-bold">How to read this curriculum map:</span> Each row represents a PLO, and each column represents a course. The color
                intensity indicates alignment strength. White dots show which faculty members address this outcome in their course.
              </p>
            </div>
            <div className="bg-white p-4 rounded shadow">
              <table className="w-full border-collapse">
                <thead>
                  <tr>
                    <th className="p-2 border border-gray-300 bg-gray-100">PLO</th>
                    {courseSequence.map((course) =>
                      activeCourse && activeCourse !== course.id ? null : (
                        <th key={course.id} className="p-2 border border-gray-300 bg-gray-100 text-center">
                          {course.fullName}
                        </th>
                      ),
                    )}
                  </tr>
                </thead>
                <tbody>
                  {PLOs.map((plo, ploIndex) =>
                    activePLO && activePLO !== plo.id ? null : (
                      <tr key={plo.id}>
                        <td className="p-2 border border-gray-300 bg-gray-50">
                          <div className="font-bold">{plo.id}</div>
                          <div className="text-sm">{plo.title}</div>
                        </td>

                        {courseSequence.map((course, courseIndex) => {
                          if (activeCourse && activeCourse !== course.id) return null;

                          // Get alignment data or use default
                          const alignmentItem = alignmentData.find((item) => item.plo === plo.id && item.course === course.id) || {
                            strength: 0.3,
                            faculty: [0.3, 0.3, 0.3, 0.3],
                          };

                          // Calculate color based on alignment strength
                          const opacity = 0.2 + alignmentItem.strength * 0.8;
                          const bgColor = `rgba(${ploIndex % 2 === 0 ? '59, 130, 246' : '239, 68, 68'}, ${opacity})`;
                          const textColor = alignmentItem.strength > 0.5 ? 'text-white' : 'text-gray-800';

                          return (
                            <td key={`${plo.id}-${course.id}`} className={`p-3 border border-gray-300 ${textColor}`} style={{ backgroundColor: bgColor }}>
                              <div className="text-center font-bold mb-2">{Math.round(alignmentItem.strength * 100)}%</div>

                              <div className="grid grid-cols-2 gap-x-2 gap-y-1 text-xs">
                                {['M&C', 'EJZ', 'WLD', 'JP'].map((faculty, i) => (
                                  <div key={faculty} className="flex items-center">
                                    <div
                                      className="w-3 h-3 rounded-full mr-1"
                                      style={{
                                        backgroundColor: alignmentItem.faculty[i] > 0 ? 'rgba(255, 255, 255, 0.9)' : 'rgba(255, 255, 255, 0.2)',
                                      }}
                                    ></div>
                                    <span>{faculty}</span>
                                  </div>
                                ))}
                              </div>
                            </td>
                          );
                        })}
                      </tr>
                    ),
                  )}
                </tbody>
              </table>
            </div>
          </div>
        );

      case 'radar':
        return (
          <div className="h-96">
            <ResponsiveContainer width="100%" height="100%">
              <RadarChart cx="50%" cy="50%" outerRadius="80%" data={radarData}>
                <PolarGrid />
                <PolarAngleAxis dataKey="name" />
                <PolarRadiusAxis angle={90} domain={[0, 100]} />
                {courseSequence.map((course, index) => (
                  <Radar key={course.id} name={course.name} dataKey={course.id} stroke={PLO_COLORS[index]} fill={PLO_COLORS[index]} fillOpacity={0.6} />
                ))}
                <Legend />
                <Tooltip />
              </RadarChart>
            </ResponsiveContainer>
          </div>
        );

      case 'progression':
        return (
          <div className="space-y-4">
            {PLOs.map((plo) => {
              if (activePLO && activePLO !== plo.id) return null;

              const ploData = getProgressionData(plo.id);
              return (
                <div key={plo.id} className="bg-gray-50 p-4 rounded">
                  <h3 className="font-semibold">{plo.title}</h3>
                  <p className="text-sm mb-4">{plo.desc}</p>
                  <div className="flex">
                    {ploData.map((item) => {
                      const course = courseSequence.find((c) => c.id === item.course);
                      return (
                        <div key={`${plo.id}-${item.course}`} className="flex-1 border-r border-gray-300 last:border-0 px-4">
                          <div className="text-sm font-bold">{course.fullName}</div>
                          <div className="bg-blue-100 h-6 w-full rounded overflow-hidden mt-2">
                            <div className="bg-blue-500 h-full" style={{ width: `${item.strength * 100}%` }} />
                          </div>
                          <div className="text-center text-xs mt-1">{(item.strength * 100).toFixed(0)}%</div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              );
            })}
          </div>
        );

      case 'bar':
        return (
          <div className="h-96">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={filteredData} margin={{ top: 20, right: 30, left: 20, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="plo" />
                <YAxis label={{ value: 'Alignment Strength', angle: -90, position: 'insideLeft' }} />
                <Tooltip content={(props) => <CustomTooltip {...props} />} />
                <Legend />
                <Bar dataKey="strength" fill="#8884d8">
                  {filteredData.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={entry.course === 'PLS-180' ? '#8884d8' : entry.course === 'PLS-280' ? '#83a6ed' : '#8dd1e1'} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        );

      default:
        return <div>Select a view</div>;
    }
  };

  return (
    <div className="p-4 max-w-full">
      <h1 className="text-2xl font-bold mb-4">Curriculum Alignment Visualization</h1>

      {/* Controls */}
      <div className="flex flex-wrap gap-4 mb-6 p-4 bg-gray-50 rounded">
        <div>
          <div className="font-medium mb-1">Visualization Type</div>
          <div className="flex flex-wrap gap-2">
            <button
              className={`px-3 py-1 rounded ${activeView === 'curriculumMap' ? 'bg-blue-600 text-white' : 'bg-gray-200'}`}
              onClick={() => setActiveView('curriculumMap')}
            >
              <span className="font-bold">Curriculum Map</span> (Overview)
            </button>
            <button className={`px-3 py-1 rounded ${activeView === 'radar' ? 'bg-blue-600 text-white' : 'bg-gray-200'}`} onClick={() => setActiveView('radar')}>
              Radar Chart (PLO Coverage)
            </button>
            <button
              className={`px-3 py-1 rounded ${activeView === 'progression' ? 'bg-blue-600 text-white' : 'bg-gray-200'}`}
              onClick={() => setActiveView('progression')}
            >
              Progression View (Course Sequence)
            </button>
            <button className={`px-3 py-1 rounded ${activeView === 'bar' ? 'bg-blue-600 text-white' : 'bg-gray-200'}`} onClick={() => setActiveView('bar')}>
              Bar Chart (Detailed Comparison)
            </button>
          </div>
        </div>

        <div>
          <div className="font-medium mb-1">Filter by PLO</div>
          <select className="border rounded px-2 py-1 w-full" value={activePLO || ''} onChange={(e) => setActivePLO(e.target.value || null)}>
            <option value="">All PLOs</option>
            {PLOs.map((plo) => (
              <option key={plo.id} value={plo.id}>
                {plo.id}: {plo.title}
              </option>
            ))}
          </select>
        </div>

        <div>
          <div className="font-medium mb-1">Filter by Course</div>
          <select className="border rounded px-2 py-1 w-full" value={activeCourse || ''} onChange={(e) => setActiveCourse(e.target.value || null)}>
            <option value="">All Courses</option>
            {courseSequence.map((course) => (
              <option key={course.id} value={course.id}>
                {course.fullName}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Main visualization area */}
      {renderSelectedView()}

      {/* Legend and explanation */}
      <div className="mt-8 p-4 bg-gray-50 rounded">
        <h2 className="text-lg font-semibold mb-2">About This Visualization</h2>
        <p className="mb-4">
          This tool helps visualize the alignment between Program Learning Outcomes (PLOs) and Course Learning Outcomes (CLOs) across the PLS sequence. The
          visualization shows how each PLO is emphasized across courses and by different faculty members.
        </p>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <h3 className="font-medium">Course Progression</h3>
            <ul className="list-disc list-inside">
              <li>
                <span className="font-medium">PLS-180:</span> Understand - Foundational concepts
              </li>
              <li>
                <span className="font-medium">PLS-280:</span> Explore - Application and exploration
              </li>
              <li>
                <span className="font-medium">PLS-380:</span> Materialize - Implementation and delivery
              </li>
            </ul>
          </div>
          <div>
            <h3 className="font-medium">Visualization Types</h3>
            <ul className="list-disc list-inside">
              <li>
                <span className="font-medium">Curriculum Map:</span> Shows PLO coverage across all courses
              </li>
              <li>
                <span className="font-medium">Radar Chart:</span> Shows PLO coverage across all courses
              </li>
              <li>
                <span className="font-medium">Progression View:</span> Shows how each PLO develops across courses
              </li>
              <li>
                <span className="font-medium">Bar Chart:</span> Compares alignment strength for selected PLOs/courses
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
};

export default CurriculumAlignment;
