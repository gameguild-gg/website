#!/usr/bin/env node

import { randomUUID } from 'node:crypto';
import { chromium } from 'playwright';

const apiBaseUrl = (process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:8080').replace(/\/$/, '');
const webBaseUrl = (process.env.COMMUNITY_ADMIN_E2E_BASE_URL ?? process.env.NEXT_PUBLIC_APP_URL ?? 'http://localhost:3011').replace(/\/$/, '');
const adminEmail = process.env.E2E_SYSTEM_ADMIN_EMAIL ?? 'admin@game-guild.com';
const adminPassword = process.env.E2E_SYSTEM_ADMIN_PASSWORD ?? 'Admin123!';
const headless = !['0', 'false', 'no'].includes((process.env.COMMUNITY_ADMIN_E2E_HEADLESS ?? 'true').toLowerCase());

function unique() {
  return `${Date.now()}-${randomUUID().replaceAll('-', '').slice(0, 8)}`;
}

async function apiRequest(path, init = {}, accessToken) {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      'content-type': 'application/json',
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
      ...init.headers,
    },
  });
  const body = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(`${init.method ?? 'GET'} ${path} failed with ${response.status}: ${JSON.stringify(body)}`);
  }
  return body;
}

async function apiStatus(path, init = {}, accessToken) {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      'content-type': 'application/json',
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
      ...init.headers,
    },
  });
  return response.status;
}

async function waitForApiState(readState, predicate, timeout = 45_000) {
  const deadline = Date.now() + timeout;
  let lastState = null;
  while (Date.now() < deadline) {
    lastState = await readState();
    if (predicate(lastState)) return lastState;
    await new Promise((resolve) => setTimeout(resolve, 150));
  }
  throw new Error(`Timed out waiting for persisted API state. Last state: ${JSON.stringify(lastState)}`);
}

async function deleteFixtureUser(userId, accessToken) {
  if (!userId) return;
  const status = await apiStatus(`/v1/users/${userId}`, { method: 'DELETE' }, accessToken);
  if (![204, 404].includes(status)) throw new Error(`DELETE /v1/users/${userId} failed with ${status}`);
}

async function findUserByEmail(email, accessToken) {
  const result = await apiRequest(`/v1/users?email=${encodeURIComponent(email)}&limit=2`, {}, accessToken);
  return result.items?.find((user) => user.email?.toLowerCase() === email.toLowerCase()) ?? null;
}

async function bootstrap() {
  const signIn = await apiRequest('/v1/auth/sign-in', {
    method: 'POST',
    body: JSON.stringify({ email: adminEmail, password: adminPassword }),
  });
  if (!signIn.tenantId) throw new Error('The administrator session does not expose a tenant id.');

  const tag = unique();
  const memberEmail = `community-admin-member-${tag}@example.test`;
  await apiRequest('/v1/auth/sign-up', {
    method: 'POST',
    body: JSON.stringify({
      username: `community_admin_member_${tag.replace(/[^a-z0-9]/gi, '_')}`,
      email: memberEmail,
      password: 'Str0ng!Passw0rd123!',
      tenantId: signIn.tenantId,
    }),
  });
  const member = await findUserByEmail(memberEmail, signIn.accessToken);
  if (!member?.id) throw new Error(`Could not resolve temporary community member ${memberEmail}.`);

  const memberships = await apiRequest(`/v1/users/${member.id}/memberships?includeInactive=true`, {}, signIn.accessToken);
  const hasActiveAdminTenantMembership = memberships.memberships?.some((membership) =>
    String(membership.tenantId).toLowerCase() === String(signIn.tenantId).toLowerCase() && membership.isActive);
  if (!hasActiveAdminTenantMembership) {
    await apiRequest(`/v1/users/${member.id}/memberships`, {
      method: 'POST',
      body: JSON.stringify({
        tenantId: signIn.tenantId,
        role: 'Member',
        requiresAcceptance: false,
        invitedByEmail: adminEmail,
        inviteeEmail: memberEmail,
        inviteeName: member.name ?? member.username ?? memberEmail,
      }),
    }, signIn.accessToken);
  }

  return {
    accessToken: signIn.accessToken,
    memberEmail,
    memberId: member.id,
    inviteEmail: `community-admin-invite-${tag}@example.test`,
    tag,
    tenantId: signIn.tenantId,
  };
}

async function assertNoErrorSurface(page, label) {
  await page.waitForLoadState('domcontentloaded');
  await page.locator('body').waitFor({ state: 'visible' });
  const body = await page.locator('body').innerText();
  if (/This page could not be found|Unhandled Runtime Error|Build Error|Application error|Internal server error/i.test(body)) {
    throw new Error(`${label} rendered an error surface at ${page.url()}:\n${body.slice(0, 1200)}`);
  }
}

async function visit(page, path, label) {
  await page.goto(`${webBaseUrl}${path}`, { waitUntil: 'domcontentloaded' });
  await assertNoErrorSurface(page, label);
  await page.waitForFunction(() => document.readyState !== 'loading');
  await page.waitForTimeout(250);
}

async function waitForText(page, value) {
  await page.getByText(value, { exact: false }).filter({ visible: true }).first().waitFor();
}

async function waitForOptionalText(page, value, timeout = 5_000) {
  await page.getByText(value, { exact: false }).filter({ visible: true }).first().waitFor({ timeout }).catch(() => undefined);
}

async function chooseOption(page, trigger, name) {
  await trigger.click();
  await page.getByRole('option', { name, exact: true }).click();
}

async function run() {
  const fixture = await bootstrap();
  const browser = await chromium.launch({ headless });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage();
  const browserErrors = [];
  const failedResponses = [];
  let inviteUserId = null;
  let groupId = null;
  let roleId = null;

  page.setDefaultTimeout(60_000);
  page.on('pageerror', (error) => browserErrors.push(error.message));
  page.on('console', (message) => {
    if (
      message.type() === 'error'
      && !/favicon|cloudflareinsights/i.test(message.text())
      && !/Failed to load resource: the server responded with a status of 404/i.test(message.text())
    ) {
      browserErrors.push(message.text());
    }
  });
  page.on('response', (response) => {
    const url = new URL(response.url());
    if (url.searchParams.has('_rsc')) return;
    if (url.origin === webBaseUrl && response.status() >= 400) failedResponses.push(`${response.status()} ${url.pathname}${url.search}`);
  });

  try {
    console.log('[community-admin-e2e] authentication');
    await visit(page, '/sign-in', 'sign in');
    await page.getByLabel('Email').fill(adminEmail);
    await page.getByLabel('Password', { exact: true }).fill(adminPassword);
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await page.waitForURL(/\/dashboard/, { timeout: 60_000 });

    console.log('[community-admin-e2e] invite lifecycle');
    await visit(page, '/dashboard/community/members/users', 'community users');
    await page.getByRole('button', { name: 'Invite User', exact: true }).click();
    await page.getByLabel('Email').fill(fixture.inviteEmail);
    await page.getByLabel('Name').fill(`Invited Member ${fixture.tag}`);
    await page.getByRole('button', { name: 'Send invite', exact: true }).click();
    await waitForText(page, `Invited Invited Member ${fixture.tag} as Member.`);
    const invitedUser = await waitForApiState(
      () => findUserByEmail(fixture.inviteEmail, fixture.accessToken),
      (user) => Boolean(user?.id),
    );
    inviteUserId = invitedUser.id;
    const pendingInviteMemberships = await apiRequest(`/v1/users/${inviteUserId}/memberships?includeInactive=true`, {}, fixture.accessToken);
    const pendingInvite = pendingInviteMemberships.memberships?.find((membership) => membership.inviteStatus === 'Pending');
    if (!pendingInvite?.tenantId) throw new Error(`Could not resolve pending invite membership for ${fixture.inviteEmail}.`);
    await apiRequest(`/v1/users/${inviteUserId}/memberships/${pendingInvite.tenantId}/invite:resend`, {
      method: 'POST',
      body: JSON.stringify({ actorEmail: adminEmail }),
    }, fixture.accessToken);
    await apiRequest(`/v1/users/${inviteUserId}/memberships/${pendingInvite.tenantId}/invite:cancel`, {
      method: 'POST',
      body: JSON.stringify({ actorEmail: adminEmail }),
    }, fixture.accessToken);
    await waitForApiState(
      () => apiRequest(`/v1/users/${inviteUserId}/memberships?includeInactive=true`, {}, fixture.accessToken),
      (result) => result.memberships?.some((membership) => membership.inviteStatus === 'Cancelled'),
    );

    console.log('[community-admin-e2e] group lifecycle and membership');
    const groupName = `E2E Community Operations ${fixture.tag}`;
    await visit(page, '/dashboard/community/members/groups', 'community groups');
    await page.getByRole('button', { name: 'Create Group', exact: true }).click();
    await page.getByLabel('Name').fill(groupName);
    await page.getByLabel('Description').fill('Temporary group proving live community administration.');
    await page.getByRole('button', { name: 'Create group', exact: true }).click();
    await waitForText(page, `Created ${groupName}.`);
    const groups = await waitForApiState(
      () => apiRequest(`/api/social/groups?search=${encodeURIComponent(groupName)}&skip=0&take=50`, {}, fixture.accessToken),
      (items) => items.some((group) => group.name === groupName),
    );
    const group = groups.find((item) => item.name === groupName);
    groupId = group.id;
    await visit(page, `/dashboard/community/members/groups/${groupId}`, 'group management');
    await chooseOption(page, page.getByLabel('User'), new RegExp(fixture.memberEmail));
    await page.getByRole('button', { name: 'Add member', exact: true }).click();
    await waitForText(page, 'Added member to group.');
    await waitForApiState(
      () => apiRequest(`/api/social/groups/${groupId}/members?skip=0&take=200`, {}, fixture.accessToken),
      (members) => members.some((member) => String(member.userId).toLowerCase() === String(fixture.memberId).toLowerCase() && member.status === 'Active'),
    );
    let memberRow = page.getByRole('row').filter({ hasText: fixture.memberEmail });
    await chooseOption(page, memberRow.getByRole('combobox'), 'Moderator');
    await memberRow.getByRole('button', { name: 'Update role' }).click();
    await waitForText(page, 'Updated member role to Moderator.');
    await waitForApiState(
      () => apiRequest(`/api/social/groups/${groupId}/members?skip=0&take=200`, {}, fixture.accessToken),
      (members) => members.some((member) => String(member.userId).toLowerCase() === String(fixture.memberId).toLowerCase() && member.role === 'Moderator'),
    );
    memberRow = page.getByRole('row').filter({ hasText: fixture.memberEmail });
    await memberRow.getByRole('button', { name: 'Remove', exact: true }).click();
    await waitForText(page, 'Removed group member.');
    await page.getByRole('button', { name: 'Archive group', exact: true }).click();
    await waitForOptionalText(page, 'Archived group.');
    await waitForApiState(
      () => apiRequest(`/api/social/groups/${groupId}`, {}, fixture.accessToken),
      (currentGroup) => currentGroup.status === 'Archived',
    );
    groupId = null;

    console.log('[community-admin-e2e] workspace promotion and demotion');
    await visit(page, '/dashboard/platform/roles', 'platform roles');
    const assignmentsCard = page.getByText('Role assignments', { exact: true }).locator('xpath=ancestor::*[@data-slot="card"][1]');
    let workspaceRow = assignmentsCard.getByRole('row').filter({ hasText: fixture.memberEmail });
    await chooseOption(page, workspaceRow.getByRole('combobox'), 'Super admin');
    await workspaceRow.getByRole('button', { name: 'Save', exact: true }).click();
    await waitForText(page, 'Updated member role to SystemAdmin.');
    await waitForApiState(
      () => apiRequest(`/v1/users/${fixture.memberId}/memberships`, {}, fixture.accessToken),
      (result) => result.memberships?.some((membership) => membership.role === 'SystemAdmin'),
    );
    const refreshedAssignmentsCard = page.getByText('Role assignments', { exact: true }).locator('xpath=ancestor::*[@data-slot="card"][1]');
    workspaceRow = refreshedAssignmentsCard.getByRole('row').filter({ hasText: fixture.memberEmail });
    await chooseOption(page, workspaceRow.getByRole('combobox'), 'Member');
    await workspaceRow.getByRole('button', { name: 'Save', exact: true }).click();
    await waitForText(page, 'Updated member role to Member.');
    await waitForApiState(
      () => apiRequest(`/v1/users/${fixture.memberId}/memberships`, {}, fixture.accessToken),
      (result) => result.memberships?.some((membership) => membership.role === 'Member'),
    );

    console.log('[community-admin-e2e] custom role CRUD and assignment');
    const roleName = `E2E Community Operator ${fixture.tag}`;
    const updatedRoleName = `${roleName} Updated`;
    await page.getByRole('button', { name: 'Create role', exact: true }).click();
    await page.getByLabel('Name').fill(roleName);
    await page.getByLabel('Description').fill('Temporary permission role for the live administration smoke.');
    await page.getByLabel('View groups').check();
    await page.getByRole('button', { name: 'Create role', exact: true }).last().click();
    await waitForText(page, `Created role ${roleName}.`);
    const roles = await waitForApiState(
      () => apiRequest('/v1/roles?includeInactive=true', {}, fixture.accessToken),
      (items) => items.some((role) => role.name === roleName),
    );
    roleId = roles.find((role) => role.name === roleName).id;
    let roleCard = page.getByRole('heading', { name: roleName, exact: true }).locator('xpath=ancestor::div[contains(@class, "rounded-lg")][1]');
    await roleCard.getByLabel('Name').fill(updatedRoleName);
    await roleCard.getByLabel('Description').fill('Updated live community administration permissions.');
    await roleCard.getByLabel('Edit groups').check();
    await roleCard.getByRole('button', { name: 'Save role', exact: true }).click();
    await waitForText(page, `Updated role ${updatedRoleName}.`);
    await waitForApiState(
      () => apiRequest(`/v1/roles/${roleId}`, {}, fixture.accessToken),
      (role) => role.name === updatedRoleName && role.permissions?.includes('groups:update'),
    );

    const customAssignmentsCard = page.getByText('Custom role assignments', { exact: true }).locator('xpath=ancestor::*[@data-slot="card"][1]');
    let customRow = customAssignmentsCard.getByRole('row').filter({ hasText: fixture.memberEmail });
    await chooseOption(page, customRow.getByRole('combobox'), updatedRoleName);
    await customRow.getByRole('button', { name: 'Assign custom role', exact: true }).click();
    await waitForText(page, 'Assigned custom role.');
    await waitForApiState(
      () => apiRequest(`/v1/roles/user/${fixture.memberId}`, {}, fixture.accessToken),
      (items) => items.some((role) => String(role.id).toLowerCase() === String(roleId).toLowerCase()),
    );
    const refreshedCustomAssignments = page.getByText('Custom role assignments', { exact: true }).locator('xpath=ancestor::*[@data-slot="card"][1]');
    customRow = refreshedCustomAssignments.getByRole('row').filter({ hasText: fixture.memberEmail });
    await customRow.getByRole('button', { name: new RegExp(`Remove ${updatedRoleName}`) }).click();
    await waitForText(page, `Removed ${updatedRoleName}.`);
    await waitForApiState(
      () => apiRequest(`/v1/roles/user/${fixture.memberId}`, {}, fixture.accessToken),
      (items) => !items.some((role) => String(role.id).toLowerCase() === String(roleId).toLowerCase()),
    );
    roleCard = page.getByRole('heading', { name: updatedRoleName, exact: true }).locator('xpath=ancestor::div[contains(@class, "rounded-lg")][1]');
    await roleCard.getByRole('button', { name: 'Delete', exact: true }).click();
    await waitForText(page, `Deleted role ${updatedRoleName}.`);
    await waitForApiState(
      () => apiStatus(`/v1/roles/${roleId}`, {}, fixture.accessToken),
      (status) => status === 404,
    );
    roleId = null;

    const meaningfulFailures = [...new Set(failedResponses)].filter((value) => !/favicon|manifest\.webmanifest/.test(value));
    if (meaningfulFailures.length > 0) throw new Error(`HTTP failures detected:\n${meaningfulFailures.join('\n')}`);
    if (browserErrors.length > 0) throw new Error(`Browser errors detected:\n${[...new Set(browserErrors)].join('\n')}`);

    console.log(`Community administration browser E2E passed for ${fixture.tag}.`);
  } catch (error) {
    const pageText = await page.locator('body').innerText().catch(() => 'Unable to read page body.');
    console.error(`[community-admin-e2e] failed at ${page.url()}`);
    console.error(`[community-admin-e2e] HTTP failures: ${[...new Set(failedResponses)].join(', ') || 'none'}`);
    console.error(`[community-admin-e2e] browser errors: ${[...new Set(browserErrors)].join(' | ') || 'none'}`);
    console.error(`[community-admin-e2e] page excerpt:\n${pageText.slice(0, 2400)}`);
    throw error;
  } finally {
    if (roleId) {
      await apiRequest('/v1/roles/:remove', {
        method: 'POST',
        body: JSON.stringify({ userId: fixture.memberId, roleId }),
      }, fixture.accessToken).catch(() => undefined);
      await apiStatus(`/v1/roles/${roleId}`, { method: 'DELETE' }, fixture.accessToken);
    }
    if (groupId) await apiStatus(`/api/social/groups/${groupId}/archive`, { method: 'POST' }, fixture.accessToken);
    await deleteFixtureUser(inviteUserId, fixture.accessToken);
    await deleteFixtureUser(fixture.memberId, fixture.accessToken);
    await browser.close();
  }
}

run().catch((error) => {
  console.error(error instanceof Error ? (error.stack ?? error.message) : error);
  process.exit(1);
});
