'use client';

import {
  completeSmsEnrollmentAction,
  completeTotpEnrollmentAction,
  disableMfaAction,
  regenerateBackupCodesAction,
  startSmsEnrollmentAction,
  startTotpEnrollmentAction,
  type MfaPageData,
} from '@/lib/auth/mfa-actions';
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
import { Input } from '@game-guild/ui/components/input';
import { Label } from '@game-guild/ui/components/label';
import { KeyRound, MessageSquare, ShieldCheck, Smartphone } from 'lucide-react';
import { useTranslations } from 'next-intl';
import * as React from 'react';
import { toast } from 'sonner';

/**
 * MFA method-management surface (settings/security, issue #244): enrolled-method list with
 * add flows for the authenticator app and SMS, recovery-code status with one-time
 * regenerate view, and password-verified disable. All mutations go through server actions
 * (mfa-actions.ts); revalidatePath refreshes the server-rendered data after each action.
 */

type MethodState =
  | { kind: 'idle' }
  | { kind: 'totp-active'; secretKey: string; qrCodeUri: string }
  | { kind: 'sms-active'; maskedPhone: string };

export function MfaMethodsManager({ data }: { data: MfaPageData }) {
  const t = useTranslations('settings.security.mfa');
  const tShared = useTranslations('settings.security');
  const [methodState, setMethodState] = React.useState<MethodState>({ kind: 'idle' });
  const [code, setCode] = React.useState('');
  const [phone, setPhone] = React.useState('');
  const [password, setPassword] = React.useState('');
  const [disableOpen, setDisableOpen] = React.useState(false);
  const [busy, setBusy] = React.useState(false);
  const [freshCodes, setFreshCodes] = React.useState<string[] | null>(null);

  const configuration = data.configuration;
  const isEnabled = configuration?.isEnabled === true;
  const methods = data.methods?.methods ?? [];
  const backupCodes = data.backupCodes;

  const totp = methods.find((m) => m.method === 'Totp');
  const sms = methods.find((m) => m.method === 'Sms');
  const backupCode = methods.find((m) => m.method === 'BackupCode');

  const reportFailure = (message?: string) => {
    toast.error(message ?? tShared('actionFailed'));
  };

  async function handleStartTotp() {
    setBusy(true);
    try {
      const result = await startTotpEnrollmentAction();
      if (result.success && result.setup?.secretKey) {
        setMethodState({ kind: 'totp-active', secretKey: result.setup.secretKey, qrCodeUri: result.setup.qrCodeUri ?? '' });
        setCode('');
        toast.success(t('totp.started'));
      } else {
        reportFailure(result.message);
      }
    } finally {
      setBusy(false);
    }
  }

  async function handleCompleteTotp() {
    if (methodState.kind !== 'totp-active') {
      return;
    }
    setBusy(true);
    try {
      const result = await completeTotpEnrollmentAction(code, methodState.secretKey);
      if (result.success) {
        setMethodState({ kind: 'idle' });
        setCode('');
        toast.success(t('totp.completed'));
      } else {
        reportFailure(result.message);
      }
    } finally {
      setBusy(false);
    }
  }

  async function handleStartSms() {
    setBusy(true);
    try {
      const result = await startSmsEnrollmentAction(phone);
      if (result.success && result.smsSetup) {
        setMethodState({ kind: 'sms-active', maskedPhone: result.smsSetup.phoneNumberMasked ?? phone });
        setCode('');
        toast.success(t('sms.sent', { phone: result.smsSetup.phoneNumberMasked ?? '' }));
      } else {
        reportFailure(result.message);
      }
    } finally {
      setBusy(false);
    }
  }

  async function handleCompleteSms() {
    setBusy(true);
    try {
      const result = await completeSmsEnrollmentAction(code);
      if (result.success) {
        setMethodState({ kind: 'idle' });
        setCode('');
        setPhone('');
        toast.success(t('sms.completed'));
      } else {
        reportFailure(result.message);
      }
    } finally {
      setBusy(false);
    }
  }

  async function handleRegenerate() {
    setBusy(true);
    try {
      const result = await regenerateBackupCodesAction();
      if (result.success) {
        setFreshCodes(result.codes ?? []);
        toast.success(t('recovery.regenerated'));
      } else {
        reportFailure(result.message);
      }
    } finally {
      setBusy(false);
    }
  }

  async function handleDisable() {
    setBusy(true);
    try {
      const result = await disableMfaAction(password);
      if (result.success) {
        setDisableOpen(false);
        setPassword('');
        setMethodState({ kind: 'idle' });
        toast.success(t('disable.success'));
      } else {
        reportFailure(result.message ?? t('disable.wrongPassword'));
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <ShieldCheck className="size-5" aria-hidden />
            {t('title')}
          </CardTitle>
          <CardDescription>{t('description')}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium">{t('status.label')}</p>
              <p className="text-muted-foreground text-sm">
                {isEnabled ? t('status.enabled', { date: configuration?.enabledAt ?? '' }) : t('status.disabled')}
              </p>
            </div>
            <Badge variant={isEnabled ? 'default' : 'secondary'} data-testid="mfa-status-badge">
              {isEnabled ? t('status.badgeEnabled') : t('status.badgeDisabled')}
            </Badge>
          </div>

          <ul className="divide-y rounded-lg border" data-testid="mfa-method-list">
            {methods.map((method) => (
              <li key={method.method} className="flex items-center justify-between gap-4 p-4">
                <div className="flex items-start gap-3">
                  {method.method === 'Totp' ? (
                    <KeyRound className="text-muted-foreground mt-0.5 size-5" aria-hidden />
                  ) : method.method === 'Sms' ? (
                    <MessageSquare className="text-muted-foreground mt-0.5 size-5" aria-hidden />
                  ) : (
                    <Smartphone className="text-muted-foreground mt-0.5 size-5" aria-hidden />
                  )}
                  <div>
                    <p className="text-sm font-medium">{method.name}</p>
                    <p className="text-muted-foreground text-sm">{method.description}</p>
                    {!method.isAvailable && method.method === 'Sms' ? <p className="text-muted-foreground text-xs">{t('sms.unavailable')}</p> : null}
                  </div>
                </div>
                <Badge variant={method.isEnabled ? 'default' : 'outline'}>
                  {method.isEnabled ? t('methods.enrolled') : method.isAvailable ? t('methods.available') : t('methods.unavailable')}
                </Badge>
              </li>
            ))}
          </ul>

          <div className="flex flex-wrap gap-2">
            {!totp?.isEnabled && (
              <Button onClick={handleStartTotp} disabled={busy} data-testid="mfa-totp-start">
                {t('totp.start')}
              </Button>
            )}
            {!sms?.isEnabled && sms?.isAvailable && methodState.kind !== 'sms-active' && (
              <Button variant="outline" onClick={() => setMethodState({ kind: 'sms-active', maskedPhone: '' })} disabled={busy} data-testid="mfa-sms-start">
                {t('sms.start')}
              </Button>
            )}
            {isEnabled && (
              <Button variant="destructive" onClick={() => setDisableOpen(true)} disabled={busy} data-testid="mfa-disable">
                {t('disable.title')}
              </Button>
            )}
          </div>

          {methodState.kind === 'totp-active' && (
            <div className="space-y-3 rounded-lg border p-4" data-testid="mfa-totp-panel">
              <p className="text-sm">{t('totp.scan')}</p>
              <p className="break-all rounded bg-muted p-2 font-mono text-xs" data-testid="mfa-totp-secret">
                {methodState.secretKey}
              </p>
              {methodState.qrCodeUri ? (
                <p className="break-all text-muted-foreground text-xs">{methodState.qrCodeUri}</p>
              ) : null}
              <div className="grid gap-2">
                <Label htmlFor="mfa-totp-code">{t('totp.codeLabel')}</Label>
                <Input
                  id="mfa-totp-code"
                  value={code}
                  onChange={(event) => setCode(event.target.value)}
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  placeholder="123456"
                />
              </div>
              <div className="flex gap-2">
                <Button onClick={handleCompleteTotp} disabled={busy || code.length < 6}>
                  {t('totp.complete')}
                </Button>
                <Button variant="ghost" onClick={() => setMethodState({ kind: 'idle' })} disabled={busy}>
                  {t('methods.cancel')}
                </Button>
              </div>
            </div>
          )}

          {methodState.kind === 'sms-active' && (
            <div className="space-y-3 rounded-lg border p-4" data-testid="mfa-sms-panel">
              <div className="grid gap-2">
                <Label htmlFor="mfa-sms-phone">{t('sms.phoneLabel')}</Label>
                <Input
                  id="mfa-sms-phone"
                  value={phone}
                  onChange={(event) => setPhone(event.target.value)}
                  inputMode="tel"
                  placeholder="+15551234567"
                />
              </div>
              <div className="flex gap-2">
                <Button onClick={handleStartSms} disabled={busy || phone.trim().length < 9}>
                  {t('sms.send')}
                </Button>
                <Button variant="ghost" onClick={() => setMethodState({ kind: 'idle' })} disabled={busy}>
                  {t('methods.cancel')}
                </Button>
              </div>
              {methodState.maskedPhone ? (
                <div className="grid gap-2">
                  <Label htmlFor="mfa-sms-code">{t('sms.codeLabel')}</Label>
                  <Input
                    id="mfa-sms-code"
                    value={code}
                    onChange={(event) => setCode(event.target.value)}
                    inputMode="numeric"
                    autoComplete="one-time-code"
                    placeholder="123456"
                  />
                  <Button onClick={handleCompleteSms} disabled={busy || code.length < 6}>
                    {t('sms.complete')}
                  </Button>
                </div>
              ) : null}
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <KeyRound className="size-5" aria-hidden />
            {t('recovery.title')}
          </CardTitle>
          <CardDescription>{t('recovery.description')}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {backupCodes?.hasBackupCodes ? (
            <p className="text-muted-foreground text-sm" data-testid="mfa-recovery-status">
              {t('recovery.remaining', { remaining: backupCodes.remainingCount, total: backupCodes.totalCount })}
            </p>
          ) : (
            <p className="text-muted-foreground text-sm">{t('recovery.none')}</p>
          )}

          {freshCodes ? (
            <div className="space-y-2 rounded-lg border p-4" data-testid="mfa-recovery-codes">
              <p className="text-sm font-medium">{t('recovery.freshWarning')}</p>
              <div className="grid grid-cols-2 gap-1 font-mono text-sm sm:grid-cols-3">
                {freshCodes.map((recoveryCode) => (
                  <span key={recoveryCode}>{recoveryCode}</span>
                ))}
              </div>
              <Button variant="outline" onClick={() => setFreshCodes(null)}>
                {t('recovery.hide')}
              </Button>
            </div>
          ) : (
            <Button variant="outline" onClick={handleRegenerate} disabled={busy} data-testid="mfa-recovery-regenerate">
              {backupCode?.isEnabled ? t('recovery.regenerate') : t('recovery.generate')}
            </Button>
          )}
        </CardContent>
      </Card>

      <AlertDialog open={disableOpen} onOpenChange={setDisableOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('disable.title')}</AlertDialogTitle>
            <AlertDialogDescription>{t('disable.description')}</AlertDialogDescription>
          </AlertDialogHeader>
          <div className="grid gap-2">
            <Label htmlFor="mfa-disable-password">{t('disable.passwordLabel')}</Label>
            <Input
              id="mfa-disable-password"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
            />
          </div>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => setPassword('')}>{t('disable.cancel')}</AlertDialogCancel>
            <AlertDialogAction onClick={handleDisable} disabled={busy || password.length === 0} data-testid="mfa-disable-confirm">
              {t('disable.confirm')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
