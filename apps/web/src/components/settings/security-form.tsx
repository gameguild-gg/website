'use client';

/**
 * 2FA security settings form.
 *
 * Two top-level states, mirroring the API's enrollment model:
 * - Not enrolled: "Set up authenticator" → QR + manual secret → code →
 *   backup codes shown exactly once → confirm returns to enrolled state.
 * - Enrolled: status card + backup-code count, regenerate (confirm),
 *   disable (code). No setup path is rendered while enrolled.
 *
 * Backup codes live in client state only until the user confirms having
 * saved them, then are discarded — the API never re-serves them.
 */

import {
  completeTotpSetup,
  disableMfa,
  regenerateBackupCodes,
  startTotpSetup,
} from '@/lib/user-settings/mfa-actions';
import { Button } from '@game-guild/ui/components/button';
import {
  Field,
  FieldDescription,
  FieldGroup,
  FieldLabel,
} from '@game-guild/ui/components/field';
import { Input } from '@game-guild/ui/components/input';
import { useTranslations } from 'next-intl';
import * as React from 'react';
import { QRCodeSVG } from 'qrcode.react';
import { toast } from 'sonner';

interface SecurityFormProps {
  initialEnabled: boolean;
  initialBackupCodesRemaining: number | null;
}

type SetupSession = {
  secretKey: string;
  qrCodeUri: string;
  expiresAt: string;
};

export function SecurityForm({
  initialEnabled,
  initialBackupCodesRemaining,
}: SecurityFormProps) {
  const t = useTranslations('settings.security');
  const [isEnabled, setIsEnabled] = React.useState(initialEnabled);
  const [remainingCount, setRemainingCount] = React.useState(initialBackupCodesRemaining);
  const [setup, setSetup] = React.useState<SetupSession | null>(null);
  const [verifyCode, setVerifyCode] = React.useState('');
  const [disableCode, setDisableCode] = React.useState('');
  const [backupCodes, setBackupCodes] = React.useState<string[] | null>(null);
  const [isPending, startTransition] = React.useTransition();

  function saveFailed() {
    toast.error(t('errors.saveFailed'));
  }

  function handleStartSetup() {
    startTransition(async () => {
      const result = await startTotpSetup();
      if (!result.ok) {
        saveFailed();
        return;
      }
      setSetup(result.data);
    });
  }

  function handleVerify() {
    if (!setup) return;
    startTransition(async () => {
      const result = await completeTotpSetup({
        code: verifyCode,
        secretKey: setup.secretKey,
      });
      if (!result.ok) {
        saveFailed();
        return;
      }
      setBackupCodes(result.data.backupCodes);
      setSetup(null);
      setVerifyCode('');
    });
  }

  function handleCodesSaved() {
    // Codes are shown once; confirming discards them from client state.
    setBackupCodes(null);
    setIsEnabled(true);
    setRemainingCount(null);
  }

  function handleRegenerate() {
    if (!window.confirm(t('regenerateConfirm'))) return;
    startTransition(async () => {
      const result = await regenerateBackupCodes();
      if (!result.ok) {
        saveFailed();
        return;
      }
      setBackupCodes(result.data.codes);
      setRemainingCount(result.data.codes.length);
    });
  }

  function handleDisable() {
    startTransition(async () => {
      const result = await disableMfa({ code: disableCode });
      if (!result.ok) {
        saveFailed();
        return;
      }
      setIsEnabled(false);
      setRemainingCount(null);
      setDisableCode('');
    });
  }

  const setupActive = setup !== null;

  return (
    <FieldGroup>
      {backupCodes !== null && backupCodes.length > 0 ? (
        <Field data-testid="backup-codes-panel">
          <FieldLabel>{t('backupCodesTitle')}</FieldLabel>
          <FieldDescription>{t('backupCodesOnce')}</FieldDescription>
          <ul
            className="mt-3 grid grid-cols-2 gap-x-6 gap-y-2 rounded-lg border p-4 font-mono text-sm sm:grid-cols-3"
            data-testid="backup-codes-list"
          >
            {backupCodes.map((code) => (
              <li key={code}>{code}</li>
            ))}
          </ul>
          <Button className="mt-4" onClick={handleCodesSaved} disabled={isPending}>
            {t('codesSaved')}
          </Button>
        </Field>
      ) : isEnabled ? (
        <>
          <Field data-testid="mfa-status">
            <FieldLabel>{t('statusEnabled')}</FieldLabel>
            {remainingCount !== null ? (
              <FieldDescription>
                {remainingCount > 0
                  ? t('backupCodesRemaining', { count: remainingCount })
                  : t('backupCodesNone')}
              </FieldDescription>
            ) : null}
            <FieldDescription>{t('recoveryHint')}</FieldDescription>
          </Field>

          <Field>
            <Button variant="outline" onClick={handleRegenerate} disabled={isPending}>
              {t('regenerate')}
            </Button>
          </Field>

          <Field data-testid="disable-panel">
            <FieldLabel>{t('disableTitle')}</FieldLabel>
            <FieldDescription>{t('disableConfirmCode')}</FieldDescription>
            <div className="mt-3 flex items-center gap-3">
              <Input
                inputMode="text"
                autoComplete="off"
                className="w-44 font-mono"
                value={disableCode}
                onChange={(event) => setDisableCode(event.target.value)}
                aria-label={t('codeLabel')}
                disabled={isPending}
              />
              <Button
                variant="destructive"
                onClick={handleDisable}
                disabled={isPending || disableCode.length === 0}
              >
                {t('disable')}
              </Button>
            </div>
          </Field>
        </>
      ) : setupActive ? (
        <Field data-testid="totp-setup-panel">
          <FieldLabel>{t('setUp')}</FieldLabel>
          <div className="mt-3 flex flex-col items-start gap-4 sm:flex-row sm:items-center">
            <div className="rounded-lg border bg-white p-3">
              <QRCodeSVG value={setup.qrCodeUri} size={160} />
            </div>
            <div className="min-w-0">
              <FieldDescription>{t('qrAlt')}</FieldDescription>
              <p className="mt-2 text-sm">{t('manualSecret')}</p>
              <code
                className="mt-1 block break-all rounded bg-muted px-2 py-1 font-mono text-sm"
                data-testid="totp-secret"
              >
                {setup.secretKey}
              </code>
              {setup.expiresAt ? (
                <p className="mt-2 text-xs text-muted-foreground">
                  {t('expiresAt', { expiresAt: new Date(setup.expiresAt).toLocaleString() })}
                </p>
              ) : null}
            </div>
          </div>
          <div className="mt-4 flex items-center gap-3">
            <Input
              inputMode="numeric"
              autoComplete="one-time-code"
              maxLength={8}
              className="w-44 font-mono"
              value={verifyCode}
              onChange={(event) => setVerifyCode(event.target.value)}
              aria-label={t('codeLabel')}
              disabled={isPending}
            />
            <Button
              onClick={handleVerify}
              disabled={isPending || verifyCode.length < 6}
            >
              {t('verify')}
            </Button>
          </div>
        </Field>
      ) : (
        <Field data-testid="mfa-status">
          <FieldLabel>{t('statusDisabled')}</FieldLabel>
          <Button className="mt-3" onClick={handleStartSetup} disabled={isPending}>
            {t('setUp')}
          </Button>
        </Field>
      )}
    </FieldGroup>
  );
}
