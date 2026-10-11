/**
 * @game-guild/client - Health Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class HealthModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Comprehensive health check endpoint for application monitoring
   *
   * Performs a comprehensive health check of all registered services and dependencies including:
   * - Database connectivity
   * - External service availability
   * - Cache systems (Redis, etc.)
   * - Message queues
   * - File systems
   * Returns HTTP 200 when all checks are healthy, HTTP 503 when any check fails.
   * Used by monitoring systems, load balancers, and orchestration platforms to determine application health.
   */
  async getApiHealth(): Promise<Result<Types.APIControllersHealthinessOutput, ApiError>> {
    const url = '/api/health';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersHealthinessOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get detailed dependency health status
   *
   * Provides detailed health information for all registered external dependencies including:
   * - Database connections (PostgreSQL, Redis, etc.)
   * - External APIs and services
   * - Message queues
   * - File storage systems
   * - Cache systems
   *
   * Each dependency includes:
   * - Current status (Healthy, Degraded, Unhealthy)
   * - Response time
   * - Connection details (sanitized)
   * - Error information if unhealthy
   */
  async getApiHealthDependencies(): Promise<Result<Types.APIControllersDependencyHealthOutput, ApiError>> {
    const url = '/api/health/dependencies';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersDependencyHealthOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Kubernetes-style liveness probe for container restart decisions
   *
   * Liveness probes determine whether the application process is running and functioning.
   * This is a lightweight check that verifies the application hasn't deadlocked or crashed.
   * Kubernetes uses this endpoint to:
   * - Restart containers that are in a broken state
   * - Detect application deadlocks or memory leaks
   * - Ensure long-running processes remain healthy
   * Always returns HTTP 200 if the process is running. Includes:
   * - Application uptime since startup
   * - Current timestamp
   * - Application version information
   * - Basic process health indicators
   * This check is intentionally simple and should not depend on external services.
   */
  async getApiLive(): Promise<Result<Types.APIControllersLivenessOutput, ApiError>> {
    const url = '/api/live';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersLivenessOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Kubernetes-style readiness probe for traffic routing decisions
   *
   * Readiness probes determine whether the application is ready to serve traffic.
   * Unlike liveness probes, readiness checks verify that all dependencies are available
   * and the application can handle requests properly.
   * Kubernetes uses this endpoint to:
   * - Remove pods from service endpoints when not ready
   * - Prevent traffic routing to initializing instances
   * - Handle rolling deployments gracefully
   * Returns HTTP 200 when ready to serve traffic, HTTP 503 when not ready.
   * Checks services tagged with "ready" in health check registration.
   */
  async getApiReady(): Promise<Result<Types.APIControllersReadinessOutput, ApiError>> {
    const url = '/api/ready';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersReadinessOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Comprehensive health check endpoint for application monitoring
   *
   * Performs a comprehensive health check of all registered services and dependencies including:
   * - Database connectivity
   * - External service availability
   * - Cache systems (Redis, etc.)
   * - Message queues
   * - File systems
   * Returns HTTP 200 when all checks are healthy, HTTP 503 when any check fails.
   * Used by monitoring systems, load balancers, and orchestration platforms to determine application health.
   */
  async getHealth(): Promise<Result<Types.APIControllersHealthinessOutput, ApiError>> {
    const url = '/health';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersHealthinessOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get detailed dependency health status
   *
   * Provides detailed health information for all registered external dependencies including:
   * - Database connections (PostgreSQL, Redis, etc.)
   * - External APIs and services
   * - Message queues
   * - File storage systems
   * - Cache systems
   *
   * Each dependency includes:
   * - Current status (Healthy, Degraded, Unhealthy)
   * - Response time
   * - Connection details (sanitized)
   * - Error information if unhealthy
   */
  async getHealthDependencies(): Promise<Result<Types.APIControllersDependencyHealthOutput, ApiError>> {
    const url = '/health/dependencies';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersDependencyHealthOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get application information including version and build details
   *
   * Provides comprehensive application information for debugging and monitoring:
   * - Application name and version
   * - Build timestamp and commit hash (if available)
   * - Runtime and framework versions
   * - Environment information
   * - Feature flags and configuration (non-sensitive)
   *
   * Useful for:
   * - Debugging version mismatches
   * - Monitoring deployments
   * - Correlating logs with specific builds
   */
  async getInfo(): Promise<Result<Types.APIControllersApplicationInfoOutput, ApiError>> {
    const url = '/info';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersApplicationInfoOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Kubernetes-style liveness probe for container restart decisions
   *
   * Liveness probes determine whether the application process is running and functioning.
   * This is a lightweight check that verifies the application hasn't deadlocked or crashed.
   * Kubernetes uses this endpoint to:
   * - Restart containers that are in a broken state
   * - Detect application deadlocks or memory leaks
   * - Ensure long-running processes remain healthy
   * Always returns HTTP 200 if the process is running. Includes:
   * - Application uptime since startup
   * - Current timestamp
   * - Application version information
   * - Basic process health indicators
   * This check is intentionally simple and should not depend on external services.
   */
  async getLive(): Promise<Result<Types.APIControllersLivenessOutput, ApiError>> {
    const url = '/live';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersLivenessOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get application metrics in Prometheus format
   *
   * Exposes application metrics in Prometheus text format for scraping by monitoring systems.
   * Metrics include:
   * - HTTP request counts and durations
   * - Database connection pool statistics
   * - Memory and CPU usage
   * - Custom business metrics
   * - Error rates and counts
   *
   * This endpoint is designed for use with Prometheus, Grafana, and other CNCF monitoring tools.
   */
  async getMetrics(): Promise<Result<void, ApiError>> {
    const url = '/metrics';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Kubernetes-style readiness probe for traffic routing decisions
   *
   * Readiness probes determine whether the application is ready to serve traffic.
   * Unlike liveness probes, readiness checks verify that all dependencies are available
   * and the application can handle requests properly.
   * Kubernetes uses this endpoint to:
   * - Remove pods from service endpoints when not ready
   * - Prevent traffic routing to initializing instances
   * - Handle rolling deployments gracefully
   * Returns HTTP 200 when ready to serve traffic, HTTP 503 when not ready.
   * Checks services tagged with "ready" in health check registration.
   */
  async getReady(): Promise<Result<Types.APIControllersReadinessOutput, ApiError>> {
    const url = '/ready';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersReadinessOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createHealthModule(client: ApiClient): HealthModule {
  return new HealthModule(client);
}
