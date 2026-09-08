import * as React from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom/vitest';
import { Button } from '../src/components/button';
import { Accordion, AccordionItem, AccordionTrigger, AccordionContent } from '../src/components/accordion';
import { Checkbox } from '../src/components/checkbox';
import { ToggleGroup, ToggleGroupItem } from '../src/components/toggle-group';
import { Dialog, DialogTrigger, DialogContent, DialogTitle } from '../src/components/dialog';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '../src/components/tabs';
import { Select, SelectTrigger, SelectContent, SelectItem, SelectValue } from '../src/components/select';
import { Switch } from '../src/components/switch';
import { DropdownMenu, DropdownMenuTrigger, DropdownMenuContent, DropdownMenuItem } from '../src/components/dropdown-menu';
import userEvent from '@testing-library/user-event';

afterEach(cleanup);

it('keeps inactive legacy forceMount tab content in the DOM', () => {
  render(<Tabs defaultValue="first"><TabsList><TabsTrigger value="first">First</TabsTrigger><TabsTrigger value="second">Second</TabsTrigger></TabsList><TabsContent value="first">One</TabsContent><TabsContent value="second" forceMount><input aria-label="Retained field" /></TabsContent></Tabs>);
  expect(screen.getByLabelText('Retained field')).toBeInTheDocument();
  expect(screen.getByLabelText('Retained field')).not.toBeVisible();
});

describe('shared UI compatibility', () => {
  it('invokes legacy menu selection once and supports preventing menu closure', async () => {
    const selected = vi.fn((event: Event) => event.preventDefault());
    render(<DropdownMenu><DropdownMenuTrigger>Actions</DropdownMenuTrigger><DropdownMenuContent><DropdownMenuItem onSelect={selected}>Mark read</DropdownMenuItem></DropdownMenuContent></DropdownMenu>);
    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'Actions' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Mark read' }));
    expect(selected).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('menu')).toBeVisible();
  });

  it('renders legacy Select item labels before the popup mounts and changes the controlled value', async () => {
    function Example() {
      const [value, setValue] = React.useState('daily');
      return <Select value={value} onValueChange={setValue}><SelectTrigger aria-label="Cadence"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="daily">Daily summary</SelectItem><SelectItem value="weekly">Weekly summary</SelectItem></SelectContent></Select>;
    }
    render(<Example />);
    expect(screen.getByRole('combobox', { name: 'Cadence' })).toHaveTextContent('Daily summary');
    const user = userEvent.setup();
    await user.click(screen.getByRole('combobox', { name: 'Cadence' }));
    await user.click(await screen.findByRole('option', { name: 'Weekly summary' }));
    expect(screen.getByRole('combobox', { name: 'Cadence' })).toHaveTextContent('Weekly summary');
  });

  it('preserves explicit Base UI items labels and disabled switch behavior', async () => {
    const changed = vi.fn();
    render(<><Select value="daily" items={[{ value: 'daily', label: 'Configured label' }]}><SelectTrigger aria-label="Configured"><SelectValue /></SelectTrigger></Select><Switch checked disabled aria-label="Locked" onCheckedChange={changed} /></>);
    expect(screen.getByRole('combobox', { name: 'Configured' })).toHaveTextContent('Configured label');
    expect(screen.getByRole('switch', { name: 'Locked' })).toHaveAttribute('aria-disabled', 'true');
    await userEvent.setup().click(screen.getByRole('switch', { name: 'Locked' }));
    expect(changed).not.toHaveBeenCalled();
  });

  it('keeps child and parent button handlers, refs and anchor semantics', () => {
    const calls: string[] = [];
    const parentRef = React.createRef<HTMLButtonElement>();
    const childRef = React.createRef<HTMLAnchorElement>();
    render(<Button asChild ref={parentRef} onClick={() => calls.push('parent')}><a href="#target" ref={childRef} onClick={() => calls.push('child')}>Visit</a></Button>);
    const link = screen.getByRole('link', { name: 'Visit' });
    fireEvent.click(link);
    expect(calls).toEqual(['child', 'parent']);
    expect(parentRef.current).toBe(link);
    expect(childRef.current).toBe(link);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('supports legacy single accordion values and non-collapsible behavior', () => {
    const onChange = vi.fn();
    render(<Accordion type="single" defaultValue="first" onValueChange={onChange}>
      <AccordionItem value="first"><AccordionTrigger>First</AccordionTrigger><AccordionContent>One</AccordionContent></AccordionItem>
      <AccordionItem value="second"><AccordionTrigger>Second</AccordionTrigger><AccordionContent>Two</AccordionContent></AccordionItem>
    </Accordion>);
    expect(screen.getByRole('button', { name: 'First' })).toHaveAttribute('aria-expanded', 'true');
    fireEvent.click(screen.getByRole('button', { name: 'First' }));
    expect(onChange).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'First' })).toHaveAttribute('aria-expanded', 'true');
    fireEvent.click(screen.getByRole('button', { name: 'Second' }));
    expect(onChange).toHaveBeenCalledWith('second');
  });

  it('preserves the newer Base UI array accordion API', () => {
    const onChange = vi.fn();
    render(<Accordion defaultValue={['first']} multiple onValueChange={onChange}>
      <AccordionItem value="first"><AccordionTrigger>First</AccordionTrigger><AccordionContent>One</AccordionContent></AccordionItem>
      <AccordionItem value="second"><AccordionTrigger>Second</AccordionTrigger><AccordionContent>Two</AccordionContent></AccordionItem>
    </Accordion>);
    fireEvent.click(screen.getByRole('button', { name: 'Second' }));
    expect(onChange.mock.calls[0]?.[0]).toEqual(['first', 'second']);
  });

  it('maps legacy indeterminate checkboxes to mixed ARIA state', () => {
    render(<Checkbox checked="indeterminate" aria-label="Select all" />);
    expect(screen.getByRole('checkbox', { name: 'Select all' })).toHaveAttribute('aria-checked', 'mixed');
  });

  it('maps legacy single toggle group callbacks to a string', () => {
    const onChange = vi.fn();
    render(<ToggleGroup type="single" defaultValue="all" onValueChange={onChange}>
      <ToggleGroupItem value="all">All</ToggleGroupItem><ToggleGroupItem value="active">Active</ToggleGroupItem>
    </ToggleGroup>);
    fireEvent.click(screen.getByRole('button', { name: 'Active' }));
    expect(onChange).toHaveBeenCalledWith('active');
  });

  it('allows an uncontrolled mixed checkbox to become checked', () => {
    render(<Checkbox defaultChecked="indeterminate" aria-label="Mixed" />);
    const checkbox = screen.getByRole('checkbox', { name: 'Mixed' });
    fireEvent.click(checkbox);
    expect(checkbox).toHaveAttribute('aria-checked', 'true');
  });

  it('runs dialog autofocus only when opened and lets escape dismissal be cancelled', async () => {
    const focus = vi.fn((event: Event) => event.preventDefault());
    const escape = vi.fn((event: Event) => event.preventDefault());
    render(<Dialog><DialogTrigger>Open</DialogTrigger><DialogContent onOpenAutoFocus={focus} onEscapeKeyDown={escape}><DialogTitle>Settings</DialogTitle><input aria-label="Name" /></DialogContent></Dialog>);
    expect(focus).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Open' }));
    await screen.findByRole('dialog');
    await waitFor(() => expect(focus).toHaveBeenCalledTimes(1));
    fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' });
    expect(escape).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});
