'use client';

import { createTransferAction, type EconomyActionResult } from '@/lib/economy/actions';
import type { FinanceEconomyContractsEconomyWalletTransaction as EconomyContractsEconomyWalletTransaction } from '@game-guild/client';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Card, CardContent, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { Input } from '@game-guild/ui/components/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@game-guild/ui/components/table';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { EconomyActionNotice, EconomyPageHeader, EconomyWorkspace, formatEconomyDate, formatEconomyUnits } from './economy-ui';

export function EconomyTransfersWorkspace({ transactions }: { transactions: EconomyContractsEconomyWalletTransaction[] }) {
  const t = useTranslations('economy');
  const router = useRouter();
  const [recipient, setRecipient] = useState('');
  const [amount, setAmount] = useState('');
  const [currency, setCurrency] = useState<'HardCoin' | 'SoftCoin'>('HardCoin');
  const [type, setType] = useState<'Tip' | 'Gift' | 'CreatorSupport'>('Tip');
  const [result, setResult] = useState<EconomyActionResult | null>(null);
  const [pending, startTransition] = useTransition();

  return (
    <EconomyWorkspace>
      <EconomyPageHeader title={t('transfers.title')} description={t('transfers.description')} />
      <EconomyActionNotice result={result} />
      <Card>
        <CardContent className="pt-6">
          <form className="grid gap-4 md:grid-cols-2 xl:grid-cols-5" onSubmit={(event) => {
            event.preventDefault();
            startTransition(async () => {
              const next = await createTransferAction(recipient, Number(amount), currency, type, crypto.randomUUID());
              setResult(next);
              if (next.success) router.refresh();
            });
          }}>
            <Field label={t('transfers.recipient')}><Input onChange={(event) => setRecipient(event.target.value)} required value={recipient} /></Field>
            <Field label={t('common.amount')}><Input inputMode="numeric" min="1" onChange={(event) => setAmount(event.target.value)} required type="number" value={amount} /></Field>
            <Field label={t('common.currency')}><select className="h-10 rounded-md border bg-background px-3 text-sm" onChange={(event) => setCurrency(event.target.value as typeof currency)} value={currency}><option>HardCoin</option><option>SoftCoin</option></select></Field>
            <Field label={t('transfers.type')}><select className="h-10 rounded-md border bg-background px-3 text-sm" onChange={(event) => setType(event.target.value as typeof type)} value={type}><option value="Tip">{t('transfers.tip')}</option><option value="Gift">{t('transfers.gift')}</option><option value="CreatorSupport">{t('transfers.creatorSupport')}</option></select></Field>
            <Button className="self-end" disabled={pending} type="submit">{t('transfers.send')}</Button>
          </form>
        </CardContent>
      </Card>
      <Card>
        <CardHeader><CardTitle>{t('transfers.history')}</CardTitle></CardHeader>
        <CardContent className="overflow-x-auto">
          <Table><TableHeader><TableRow><TableHead>{t('common.identifier')}</TableHead><TableHead>{t('common.amount')}</TableHead><TableHead>{t('common.state')}</TableHead><TableHead>{t('common.created')}</TableHead></TableRow></TableHeader>
            <TableBody>{transactions.map((entry) => <TableRow key={entry.journalEntryId}><TableCell className="font-mono text-sm">{entry.journalEntryId}</TableCell><TableCell>{formatEconomyUnits(entry.amountUnits)} {entry.currency}</TableCell><TableCell><Badge variant="secondary">{entry.templateKind}</Badge></TableCell><TableCell>{formatEconomyDate(entry.recordedAt)}</TableCell></TableRow>)}</TableBody>
          </Table>
        </CardContent>
      </Card>
    </EconomyWorkspace>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return <label className="flex flex-col gap-2 text-sm font-medium">{label}{children}</label>;
}
