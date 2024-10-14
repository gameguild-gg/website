"use client";

import React, { useState } from 'react';
import {
    BarChart,
    Bar,
    XAxis,
    YAxis,
    CartesianGrid,
    Tooltip,
    ResponsiveContainer,
    LabelList,
} from 'recharts';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import { ScrollArea } from '@/components/ui/scroll-area';

// Function to generate random data for the graphs
const generateData = (max: number, days: number) => {
    const data: { day: number; downloads: number; views: number; followers: number }[] = [];
    for (let i = 0; i < days; i++) {
        data.push({
            day: i + 1,
            downloads: Math.floor(Math.random() * max),
            views: Math.floor(Math.random() * max),
            followers: Math.floor(Math.random() * max),
        });
    }
    return data;
};

// Function to set the Y-axis domain based on the maximum value
const getYAxisDomain = (value: number) => {
    const scale = Math.ceil(value / 10) * 10;
    return [0, scale];
};

// Graph component that renders the bar chart for a specific data set
const Graph = ({
                   data,
                   dataKey,
                   color,
                   title,
               }: {
    data: any[];
    dataKey: string;
    color: string;
    title: string;
}) => {
    const maxValue = Math.max(...data.map((item) => item[dataKey]));
    const [yAxisDomain, setYAxisDomain] = useState(getYAxisDomain(maxValue));

    return (
        <div className="mb-6">
            <h3 className="text-lg font-semibold mb-2 text-white">{title}</h3>
            <ResponsiveContainer width="100%" height={300}>
                <BarChart data={data} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="day" />
                    <YAxis domain={yAxisDomain} />
                    <Tooltip />
                    <Bar dataKey={dataKey} fill={color}>
                        <LabelList dataKey={dataKey} position="top" fill="#fff" fontSize={12} />
                    </Bar>
                </BarChart>
            </ResponsiveContainer>
        </div>
    );
};

export default function AnalyticsGraphs() {
    const [timePeriod, setTimePeriod] = useState('30d');
    const [data, setData] = useState(generateData(100, 30));

    // Handle time period change for the graphs
    const handleTimePeriodChange = (value: string) => {
        setTimePeriod(value);
        const days = value === '7d' ? 7 : value === '30d' ? 30 : 90;
        const max = value === '7d' ? 50 : value === '30d' ? 100 : 200;
        setData(generateData(max, days));
    };

    return (
        <div className="space-y-6">
            <div className="flex justify-end">
                <Select onValueChange={handleTimePeriodChange} defaultValue={timePeriod}>
                    <SelectTrigger className="w-[180px] bg-gray-800 text-white border border-gray-600 rounded">
                        <SelectValue placeholder="Select time period" />
                    </SelectTrigger>
                    <SelectContent className="bg-gray-800 text-white">
                        <SelectItem value="7d">Last 7 days</SelectItem>
                        <SelectItem value="30d">Last 30 days</SelectItem>
                        <SelectItem value="90d">Last 90 days</SelectItem>
                    </SelectContent>
                </Select>
            </div>
            <ScrollArea className="h-[calc(100vh-300px)]">
                <div className="space-y-12 pr-4">
                    <Graph data={data} dataKey="downloads" color="red" title="Downloads" />
                    <Graph data={data} dataKey="views" color="red" title="Views" />
                    <Graph data={data} dataKey="followers" color="red" title="Followers" />
                </div>
            </ScrollArea>
        </div>
    );
}
