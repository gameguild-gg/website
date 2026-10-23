'use client';

import {
  renameSessionAction,
  revokeTrustedDeviceAction,
  terminateAllSessionsAction,
  terminateOtherSessionsAction,
  terminateSessionAction,
  trustCurrentDeviceAction,
  type SessionsPageData,
} from '@/lib/auth/sessions-actions';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@game-guild/ui/components/card';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@game-guild/ui/components/alert-dialog';
import { Monitor, Smartphone, Tablet } from 'lucide-react';
import { useTranslations } from 'next-intl';
import * as React from 'react';
import { toast } from 'sonner';

/**
 * Session management surface (settings/security, issue #278): active session list
 * with device/location metadata, per-session custom naming, single/bulk termination,
 * the current-session security analysis, and the trusted-device list.
 *
 * All mutations go through server actions (sessions-actions.ts); revalidatePath
 * refreshes the server-rendered data after every action.
 */

const RISK_TONE: Record<string, 'default' | 'secondary' | 'destructive'> = {
  low: 'secondary',
  medium: 'default',
  high: 'destructive',
  critical: 'destructive',
};

const subscribeToHydration = () => () => undefined;

function formatTimestamp(iso: string | null | undefined, locale: string): string {
  if (!iso) {
    return '—';
  }
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) {
    return '—';
  }
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(parsed);
}

function DeviceIcon({ isMobile }: { isMobile: boolean | undefined }) {
  if (isMobile) {
    return <Smartphone className="size-4 shrink-0" aria-hidden />;
  }
  return <Monitor className="size-4 shrink-0" aria-hidden />;
}

function deviceSummary(
  session: SessionsPageData['sessions'][number],
): { name: string; detail: string | undefined } {
  const info = session.deviceInfo;
  const name =
    info?.deviceName?.trim() ||
    [info?.deviceType, info?.operatingSystem].filter(Boolean).join(' · ') ||
    'Unknown device';
  const detail = [info?.browser, info?.browserVersion].filter(Boolean).join(' ');
  return { name, detail: detail || undefined };
}

function riskTone(riskLevel: string | null | undefined) {
  if (riskLevel && RISK_TONE[riskLevel.toLowerCase()]) {
    return RISK_TONE[riskLevel.toLowerCase()];
  }
  return 'default' as const;
}

export function SessionsManager({ data, locale }: { data: SessionsPageData; locale: string }) {
  const t = useTranslations('settings.security');
  const [isPending, startTransition] = React.useTransition();
  // Server HTML is visible before React installs the action handlers. Keep these
  // controls disabled until hydration so an early click cannot silently disappear.
  const isHydrated = React.useSyncExternalStore(subscribeToHydration, () => true, () => false);
  const isUnavailable = isPending || !isHydrated;
  const [renamingId, setRenamingId] = React.useState<string | null>(null);
  const [renameValue, setRenameValue] = React.useState('');
  const [confirmTerminateAll, setConfirmTerminateAll] = React.useState(false);

  function run(action: () => Promise<{ success: boolean }>, successMessage: string) {
    startTransition(async () => {
      const result = await action();
      if (result.success) {
        toast.success(successMessage);
      } else {
        toast.error(t('actionFailed'));
      }
    });
  }

  function submitRename(sessionId: string) {
    const name = renameValue.trim();
    if (name.length < 1) {
      toast.error(t('rename.nameRequired'));
      return;
    }
    if (name.length > 64) {
      toast.error(t('rename.nameTooLong'));
      return;
    }
    setRenamingId(null);
    run(() => renameSessionAction(sessionId, name), t('rename.success'));
  }

  const analysis = data.analysis;
  const currentSession = data.sessions.find((session) => session.isCurrent);

  return (
    <div className="space-y-6">
      {analysis ? (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between gap-2">
              <span>{t('analysis.title')}</span>
              <Badge variant={riskTone(analysis.riskLevel)}>
                {t(`analysis.risk.${(analysis.riskLevel ?? 'low').toLowerCase()}`)}
              </Badge>
            </CardTitle>
            <CardDescription>
              {t('analysis.description', {
                sessions: analysis.activeSessionCount ?? data.sessions.length,
                devices: analysis.totalDeviceCount ?? 0,
              })}
            </CardDescription>
          </CardHeader>
          {(analysis.riskFactors?.length ?? 0) > 0 && (
            <CardContent>
              <ul className="list-disc space-y-1 pl-5 text-sm text-muted-foreground">
                {analysis.riskFactors!.map((factor) => (
                  <li key={factor}>{factor}</li>
                ))}
              </ul>
            </CardContent>
          )}
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>{t('sessions.title')}</CardTitle>
          <CardDescription>{t('sessions.description')}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <ul className="space-y-3">
            {data.sessions.length === 0 && <li className="text-sm text-muted-foreground">{t('sessions.empty')}</li>}
            {data.sessions.map((session) => {
              const { name, detail } = deviceSummary(session);
              const location = session.location?.displayLocation;
              return (
                <li
                  key={session.id}
                  data-testid={`session-row-${session.id}`}
                  className="flex flex-col gap-3 rounded-lg border p-4 sm:flex-row sm:items-center sm:justify-between"
                >
                  <div className="flex min-w-0 items-start gap-3">
                    <span className="mt-0.5 text-muted-foreground">
                      <DeviceIcon isMobile={session.deviceInfo?.isMobile} />
                    </span>
                    <div className="min-w-0">
                      <p className="flex flex-wrap items-center gap-2 text-sm font-medium">
                        <span className="truncate">{name}</span>
                        {session.isCurrent && <Badge data-testid="current-session-badge">{t('sessions.current')}</Badge>}
                        {session.isTrustedDevice && <Badge variant="secondary">{t('sessions.trusted')}</Badge>}
                      </p>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {[detail, location, session.ipAddress].filter(Boolean).join(' · ')}
                      </p>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {t('sessions.lastUsed', {
                          time: formatTimestamp(session.lastUsedAt, locale),
                        })}
                      </p>
                    </div>
                  </div>
                  <div className="flex shrink-0 items-center gap-2">
                    {renamingId === session.id ? (
                      <div className="flex items-center gap-2">
                        <label className="sr-only" htmlFor={`rename-${session.id}`}>
                          {t('rename.label')}
                        </label>
                        <input
                          id={`rename-${session.id}`}
                          value={renameValue}
                          onChange={(event) => setRenameValue(event.target.value)}
                          onKeyDown={(event) => {
                            if (event.key === 'Enter') {
                              submitRename(session.id!);
                            }
                            if (event.key === 'Escape') {
                              setRenamingId(null);
                            }
                          }}
                          maxLength={64}
                          disabled={isUnavailable}
                          className="h-8 w-40 rounded-md border bg-transparent px-2 text-sm"
                          data-testid={`rename-input-${session.id}`}
                        />
                        <Button
                          size="sm"
                          disabled={isUnavailable}
                          onClick={() => submitRename(session.id!)}
                          data-testid={`rename-save-${session.id}`}
                        >
                          {t('rename.save')}
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          disabled={isUnavailable}
                          onClick={() => setRenamingId(null)}
                        >
                          {t('rename.cancel')}
                        </Button>
                      </div>
                    ) : (
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={isUnavailable}
                        onClick={() => {
                          setRenamingId(session.id!);
                          setRenameValue(session.deviceInfo?.deviceName ?? '');
                        }}
                        aria-label={t('rename.aria', { name })}
                      >
                        {t('rename.trigger')}
                      </Button>
                    )}
                    {!session.isCurrent && (
                      <Button
                        size="sm"
                        variant="destructive"
                        disabled={isUnavailable}
                        onClick={() => run(() => terminateSessionAction(session.id!), t('sessions.terminated'))}
                        aria-label={t('sessions.terminateAria', { name })}
                      >
                        {t('sessions.terminate')}
                      </Button>
                    )}
                  </div>
                </li>
              );
            })}
          </ul>
          <div className="flex flex-wrap gap-2 border-t pt-4">
            <Button
              variant="outline"
              disabled={isUnavailable || data.sessions.length <= 1}
              onClick={() => run(() => terminateOtherSessionsAction(), t('sessions.othersTerminated'))}
            >
              {t('sessions.terminateOthers')}
            </Button>
            <Button
              variant="destructive"
              disabled={isUnavailable}
              onClick={() => setConfirmTerminateAll(true)}
            >
              {t('sessions.terminateAll')}
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('trustedDevices.title')}</CardTitle>
          <CardDescription>{t('trustedDevices.description')}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <ul className="space-y-3">
            {data.trustedDevices.length === 0 && (
              <li className="text-sm text-muted-foreground">{t('trustedDevices.empty')}</li>
            )}
            {data.trustedDevices.map((device) => (
              <li
                key={device.id}
                data-testid={`trusted-device-row-${device.id}`}
                className="flex items-center justify-between gap-3 rounded-lg border p-4"
              >
                <div className="flex min-w-0 items-start gap-3">
                  <Tablet className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium">
                      {device.deviceName || device.deviceInfo?.deviceName || t('trustedDevices.unnamed')}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {t('trustedDevices.trustedAt', {
                        time: formatTimestamp(device.trustedAt, locale),
                      })}
                      {device.expiresAt
                        ? ` · ${t('trustedDevices.expiresAt', { time: formatTimestamp(device.expiresAt, locale) })}`
                        : ''}
                    </p>
                  </div>
                </div>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={isUnavailable}
                  onClick={() => run(() => revokeTrustedDeviceAction(device.id!), t('trustedDevices.revoked'))}
                >
                  {t('trustedDevices.revoke')}
                </Button>
              </li>
            ))}
          </ul>
          {currentSession && !currentSession.isTrustedDevice && (
            <div className="flex items-center justify-between gap-3 rounded-lg border p-4">
              <div>
                <p className="text-sm font-medium">{t('trustedDevices.trustCurrent')}</p>
                <p className="mt-1 text-xs text-muted-foreground">{t('trustedDevices.trustCurrentDescription')}</p>
              </div>
              <Button
                size="sm"
                variant="outline"
                disabled={isUnavailable}
                onClick={() => run(() => trustCurrentDeviceAction(), t('trustedDevices.trustedCurrent'))}
              >
                {t('trustedDevices.trustCurrentButton')}
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      <AlertDialog open={confirmTerminateAll} onOpenChange={setConfirmTerminateAll}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('sessions.confirmTerminateAllTitle')}</AlertDialogTitle>
            <AlertDialogDescription>{t('sessions.confirmTerminateAllDescription')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isUnavailable}>{t('sessions.confirmCancel')}</AlertDialogCancel>
            <AlertDialogAction
              disabled={isUnavailable}
              onClick={() => run(() => terminateAllSessionsAction(), t('sessions.allTerminated'))}
            >
              {t('sessions.confirmTerminateAll')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
