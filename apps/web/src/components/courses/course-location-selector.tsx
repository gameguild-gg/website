'use client';

import { Badge } from '@game-guild/ui/components/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@game-guild/ui/components/select';
import { getTestingLocations } from '@/lib/api/testing-lab';
import type { TestingLocation as TestingLocationFull } from '@/lib/api/testing-types';
import { AlertCircle, CheckCircle, FolderOpen, MapPin, Users } from 'lucide-react';
import { useEffect, useState } from 'react';

interface CourseLocationSelectorProps {
  onLocationSelected: (location: TestingLocationFull, maxTests: number, maxProjects: number) => void;
}

export function CourseLocationSelector({ onLocationSelected }: CourseLocationSelectorProps) {
  const [locations, setLocations] = useState<TestingLocationFull[]>([]);
  const [selectedLocation, setSelectedLocation] = useState<TestingLocationFull | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingContent, setIsLoadingContent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadLocations = async () => {
      try {
        setIsLoading(true);
        setError(null);
        const testingLocations = await getTestingLocations(0, 50);

        // Filter to active locations only.
        const activeLocations = testingLocations.filter((location) => location.status === 1); // Active status

        setLocations(activeLocations);
      } catch (err) {
        console.error('Failed to load testing locations:', err);
        setError('Failed to load testing locations');
      } finally {
        setIsLoading(false);
      }
    };

    loadLocations();
  }, []);

  const handleLocationChange = async (locationId: string) => {
    const location = locations.find((loc) => loc.id === locationId);
    if (!location) return;

    setSelectedLocation(location);
    setIsLoadingContent(true);

    try {
      // Location selection drives the maximum content loading
      const maxTests = location.maxTestersCapacity;
      const maxProjects = location.maxProjectsCapacity;

      // Notify parent component with location and capacity limits
      onLocationSelected(location, maxTests, maxProjects);
    } finally {
      setIsLoadingContent(false);
    }
  };

  const getLocationStatusColor = (status: number) => {
    switch (status) {
      case 1:
        return 'bg-green-500/20 text-green-400 border-green-500/30'; // Active
      case 2:
        return 'bg-yellow-500/20 text-yellow-400 border-yellow-500/30'; // Maintenance
      default:
        return 'bg-gray-500/20 text-gray-400 border-gray-500/30'; // Inactive
    }
  };

  const getLocationStatusText = (status: number) => {
    switch (status) {
      case 1:
        return 'Active';
      case 2:
        return 'Maintenance';
      default:
        return 'Inactive';
    }
  };

  if (isLoading) {
    return (
      <Card className="bg-gray-900 border-gray-800">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <MapPin className="h-5 w-5" />
            Testing Location
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="animate-pulse">
            <div className="h-10 bg-gray-700 rounded mb-4"></div>
            <div className="h-4 bg-gray-700 rounded w-3/4"></div>
          </div>
        </CardContent>
      </Card>
    );
  }

  if (error) {
    return (
      <Card className="bg-gray-900 border-gray-800">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <MapPin className="h-5 w-5" />
            Testing Location
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-center gap-2 text-red-400">
            <AlertCircle className="h-4 w-4" />
            <span className="text-sm">{error}</span>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="bg-gray-900 border-gray-800">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <MapPin className="h-5 w-5" />
          Select Testing Location
        </CardTitle>
        <p className="text-sm text-gray-400">Choose a testing location to determine maximum available tests and projects</p>
      </CardHeader>
      <CardContent className="space-y-4">
        <div>
          <Select onValueChange={(value: string | null) => value !== null && handleLocationChange(value)} disabled={isLoadingContent}>
            <SelectTrigger>
              <SelectValue placeholder="Choose a testing location..." />
            </SelectTrigger>
            <SelectContent>
              {locations.map((location) => (
                <SelectItem key={location.id} value={location.id || ''}>
                  <div className="flex items-center gap-2 w-full">
                    <span className="flex-1">{location.name}</span>
                    <Badge className={getLocationStatusColor(location.status)}>{getLocationStatusText(location.status)}</Badge>
                  </div>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {selectedLocation && (
          <div className="space-y-4">
            <div className="p-4 bg-gray-800/50 rounded-lg border border-gray-700">
              <div className="flex items-start justify-between mb-3">
                <div>
                  <h4 className="font-medium text-white">{selectedLocation.name}</h4>
                  {selectedLocation.description && <p className="text-sm text-gray-400 mt-1">{selectedLocation.description}</p>}
                  {selectedLocation.address && <p className="text-xs text-gray-500 mt-1">{selectedLocation.address}</p>}
                </div>
                <Badge className={getLocationStatusColor(selectedLocation.status)}>{getLocationStatusText(selectedLocation.status)}</Badge>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="bg-blue-500/10 border border-blue-500/20 rounded-lg p-3">
                  <div className="flex items-center gap-2 text-blue-400">
                    <Users className="h-4 w-4" />
                    <span className="font-medium text-lg">{selectedLocation.maxTestersCapacity}</span>
                  </div>
                  <p className="text-xs text-blue-300 mt-1">Max Testers</p>
                </div>

                <div className="bg-green-500/10 border border-green-500/20 rounded-lg p-3">
                  <div className="flex items-center gap-2 text-green-400">
                    <FolderOpen className="h-4 w-4" />
                    <span className="font-medium text-lg">{selectedLocation.maxProjectsCapacity}</span>
                  </div>
                  <p className="text-xs text-green-300 mt-1">Max Projects</p>
                </div>
              </div>

              {selectedLocation.equipmentAvailable && (
                <div className="mt-3 pt-3 border-t border-gray-700">
                  <p className="text-xs text-gray-400">
                    <strong>Equipment:</strong> {selectedLocation.equipmentAvailable}
                  </p>
                </div>
              )}
            </div>

            <div className="flex items-center gap-2 text-green-400 text-sm">
              <CheckCircle className="h-4 w-4" />
              <span>
                Location selected. Content will be limited to {selectedLocation.maxTestersCapacity} tests and {selectedLocation.maxProjectsCapacity} projects.
              </span>
            </div>

            {isLoadingContent && (
              <div className="flex items-center gap-2 text-blue-400 text-sm">
                <div className="animate-spin h-4 w-4 border-2 border-blue-400 border-t-transparent rounded-full"></div>
                <span>Loading content based on location capacity...</span>
              </div>
            )}
          </div>
        )}

        {locations.length === 0 && (
          <div className="text-center py-4">
            <p className="text-sm text-gray-400">No active testing locations available</p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
