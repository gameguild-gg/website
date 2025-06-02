'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { Label } from '@/components/ui/label';
import { Bold, CircleHelp, Code, ExternalLink, HelpCircle, ImageIcon, Italic, Link2, List, ListOrdered, PlusIcon, X } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';

import { Tabs, TabsContent } from '@/components/ui/tabs';
import { createProject, Project } from '@/components/projects/actions';
import { HoverCard, HoverCardContent, HoverCardTrigger } from '@/components/ui/hover-card';

interface NewProjectFormProps {
  onProjectCreated: (project: Project) => void;
}

export function CreateProjectForm({ onProjectCreated }: NewProjectFormProps) {
  const [isOpen, setIsOpen] = useState(false);
  const router = useRouter();
  const [formData, setFormData] = useState({
    title: '',
    projectUrl: '',
    tagline: '',
    classification: 'game',
    kind: 'downloadable',
    releaseStatus: 'released',
    pricing: 'donate',
    suggestedDonation: '2.00',
    description: '',
    tags: '',
    aiDisclosure: 'no',
    stores: {
      steam: false,
      apple: false,
      google: false,
      amazon: false,
      windows: false,
    },
    customNoun: '',
    community: 'disabled',
    visibility: 'draft',
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const newProject = await createProject(formData.title, 'not-started');
    onProjectCreated(newProject);
    setIsOpen(false);
    router.push(`/projects/${encodeURIComponent(newProject.id)}`);
  };

  return (
    <Sheet open={isOpen} onOpenChange={setIsOpen}>
      <SheetTrigger asChild>
        <Button variant="outline" className="gap-2">
          <PlusIcon className="size-4" />
          Add Project
        </Button>
      </SheetTrigger>
      <SheetContent className="min-w-full">
        <TooltipProvider>
          <div className="flex flex-col flex-1 max-w-4xl md:p-20">
            <SheetHeader className="flex flex-row items-center gap-6">
              <button className="flex items-center justify-center">
                <X className="size-6 text-muted-foreground" />
              </button>
              <SheetTitle className="flex items-center justify-center text-3xl font-normal align-middle">Create a project</SheetTitle>
            </SheetHeader>
            <div className="md:p-16">
              <form onSubmit={handleSubmit} className="flex flex-col flex-1">
                <Tabs defaultValue="basic" className="flex flex-col flex-1">
                  <TabsContent value="basic" className="py-6 space-y-6">
                    <div className="flex flex-col gap-12">
                      <Label htmlFor="title" className="flex flex-col items-start justify-center text-4xl align-middle font-bold leading-tight">
                        <span>Let’s start with a name for</span>
                        <div className="flex flex-row items-center justify-center gap-2">
                          <span>your project</span>
                          <HoverCard>
                            <HoverCardTrigger asChild>
                              <CircleHelp className="size-4 text-muted-foreground" />
                            </HoverCardTrigger>
                            <HoverCardContent side="right">
                              <div className="flex flex-col gap-2">
                                <p className="text-sm text-zinc-500">This is the name of your project.</p>
                                <p className="text-sm text-zinc-500">It will be visible to anyone who can see your project.</p>
                                <p className="text-sm text-zinc-500">You can change it later.</p>
                              </div>
                            </HoverCardContent>
                          </HoverCard>
                        </div>
                      </Label>
                      <Input
                        id="title"
                        value={formData.title}
                        onChange={(e) => setFormData((prev) => ({ ...prev, title: e.target.value }))}
                        required
                        placeholder="Enter your project name"
                        className="font-semibold min-h-max bg-transparent dark:bg-transparent border-0 border-b-2 rounded-none text-4xl md:text-4xl placeholder:text-4xl p-0 m-0"
                      />
                    </div>
                    <div>
                      <Label htmlFor="projectUrl" className="text-zinc-400">
                        Project URL
                      </Label>
                      <Input
                        id="projectUrl"
                        value={formData.projectUrl}
                        onChange={(e) => setFormData((prev) => ({ ...prev, projectUrl: e.target.value }))}
                        className="mt-1.5 bg-zinc-900 border-zinc-800"
                        placeholder="https://"
                      />
                    </div>

                    <div>
                      <Label htmlFor="tagline" className="text-zinc-400">
                        Short description or tagline
                        <span className="block text-sm text-zinc-500">Shown when we link to your project. Avoid duplicating your project&apos;s title</span>
                      </Label>
                      <Input
                        id="tagline"
                        value={formData.tagline}
                        onChange={(e) => setFormData((prev) => ({ ...prev, tagline: e.target.value }))}
                        className="mt-1.5 bg-zinc-900 border-zinc-800"
                        placeholder="Optional"
                      />
                    </div>

                    <div>
                      <Label htmlFor="classification" className="text-zinc-400">
                        Classification
                        <span className="block text-sm text-zinc-500">What are you uploading?</span>
                      </Label>
                      <Select value={formData.classification} onValueChange={(value) => setFormData((prev) => ({ ...prev, classification: value }))}>
                        <SelectTrigger className="mt-1.5 bg-zinc-900 border-zinc-800">
                          <SelectValue placeholder="Select classification" />
                        </SelectTrigger>
                        <SelectContent className="bg-zinc-900 border-zinc-800">
                          <SelectItem value="game">Games — A piece of software you can play</SelectItem>
                          <SelectItem value="tool">Tools — Software for creating</SelectItem>
                          <SelectItem value="assets">Assets — Resources for creators</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                  </TabsContent>
                  <TabsContent value="details" className="space-y-6">
                    <div>
                      <Label htmlFor="kind" className="text-zinc-400">
                        Kind of project
                      </Label>
                      <Select value={formData.kind} onValueChange={(value) => setFormData((prev) => ({ ...prev, kind: value }))}>
                        <SelectTrigger className="mt-1.5 bg-zinc-900 border-zinc-800">
                          <SelectValue placeholder="Select kind" />
                        </SelectTrigger>
                        <SelectContent className="bg-zinc-900 border-zinc-800">
                          <SelectItem value="downloadable">Downloadable — You only have files to be downloaded</SelectItem>
                          <SelectItem value="html">HTML — Runs in the browser</SelectItem>
                          <SelectItem value="unity">Unity — Made with Unity</SelectItem>
                        </SelectContent>
                      </Select>
                      <p className="text-xs text-zinc-500 mt-1 italic">You can add additional downloadable files for any of the types above</p>
                    </div>

                    <div>
                      <Label htmlFor="releaseStatus" className="text-zinc-400">
                        Release status
                      </Label>
                      <Select value={formData.releaseStatus} onValueChange={(value) => setFormData((prev) => ({ ...prev, releaseStatus: value }))}>
                        <SelectTrigger className="mt-1.5 bg-zinc-900 border-zinc-800">
                          <SelectValue placeholder="Select status" />
                        </SelectTrigger>
                        <SelectContent className="bg-zinc-900 border-zinc-800">
                          <SelectItem value="released">Released — Project is complete, but might receive updates</SelectItem>
                          <SelectItem value="in-progress">In Progress — Project is under active development</SelectItem>
                          <SelectItem value="prototype">Prototype — Early prototype or proof of concept</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>

                    {/*<div className="space-y-4">*/}
                    {/*  <Label className="text-zinc-400">Pricing</Label>*/}
                    {/*  <RadioGroup*/}
                    {/*    defaultValue="donate"*/}
                    {/*    value={formData.pricing}*/}
                    {/*    onValueChange={(value) => setFormData((prev) => ({ ...prev, pricing: value }))}*/}
                    {/*    className="grid gap-4"*/}
                    {/*  >*/}
                    {/*    <div className="flex items-center space-x-2">*/}
                    {/*      <RadioGroupItem value="donate" id="donate" className="border-zinc-800" />*/}
                    {/*      <Label htmlFor="donate" className="text-zinc-400">*/}
                    {/*        $0 or donate*/}
                    {/*      </Label>*/}
                    {/*    </div>*/}
                    {/*    <div className="flex items-center space-x-2">*/}
                    {/*      <RadioGroupItem value="paid" id="paid" className="border-zinc-800" />*/}
                    {/*      <Label htmlFor="paid" className="text-zinc-400">*/}
                    {/*        Paid*/}
                    {/*      </Label>*/}
                    {/*    </div>*/}
                    {/*    <div className="flex items-center space-x-2">*/}
                    {/*      <RadioGroupItem value="no-payments" id="no-payments" className="border-zinc-800" />*/}
                    {/*      <Label htmlFor="no-payments" className="text-zinc-400">*/}
                    {/*        No payments*/}
                    {/*      </Label>*/}
                    {/*    </div>*/}
                    {/*  </RadioGroup>*/}
                    {/*  <p className="text-sm text-zinc-500">*/}
                    {/*    Someone downloading your project will be asked for a donation before getting access.*/}
                    {/*    <br />*/}
                    {/*    They can skip to download for free.*/}
                    {/*  </p>*/}
                    {/*</div>*/}

                    {/*{formData.pricing === 'donate' && (*/}
                    {/*  <div>*/}
                    {/*    <Label htmlFor="suggestedDonation" className="text-zinc-400">*/}
                    {/*      Suggested donation*/}
                    {/*      <span className="block text-sm text-zinc-500">Default donation amount</span>*/}
                    {/*    </Label>*/}
                    {/*    <Input*/}
                    {/*      id="suggestedDonation"*/}
                    {/*      type="number"*/}
                    {/*      min="0"*/}
                    {/*      step="0.01"*/}
                    {/*      value={formData.suggestedDonation}*/}
                    {/*      onChange={(e) => setFormData((prev) => ({ ...prev, suggestedDonation: e.target.value }))}*/}
                    {/*      className="mt-1.5 bg-zinc-900 border-zinc-800 w-32"*/}
                    {/*    />*/}
                    {/*  </div>*/}
                    {/*)}*/}

                    {/*<div>*/}
                    {/*  <Label className="text-zinc-400">Uploads</Label>*/}
                    {/*  <div className="mt-4 space-y-4">*/}
                    {/*    <div className="flex items-center gap-2">*/}
                    {/*      <Button className="bg-pink-600 hover:bg-pink-500">*/}
                    {/*        <Upload className="w-4 h-4 mr-2" />*/}
                    {/*        Upload files*/}
                    {/*      </Button>*/}
                    {/*      <span className="text-zinc-400">or</span>*/}
                    {/*      <Button variant="outline" className="bg-zinc-900 border-zinc-800">*/}
                    {/*        Choose from Dropbox*/}
                    {/*      </Button>*/}
                    {/*      <Button variant="link" className="text-zinc-400">*/}
                    {/*        Add External file*/}
                    {/*      </Button>*/}
                    {/*      <Tooltip>*/}
                    {/*        <TooltipTrigger>*/}
                    {/*          <HelpCircle className="h-4 w-4 text-zinc-400" />*/}
                    {/*        </TooltipTrigger>*/}
                    {/*        <TooltipContent>*/}
                    {/*          <p>Add files from external sources</p>*/}
                    {/*        </TooltipContent>*/}
                    {/*      </Tooltip>*/}
                    {/*    </div>*/}
                    {/*    <p className="text-sm text-zinc-500">*/}
                    {/*      File size limit: 1 GB.{' '}*/}
                    {/*      <Button variant="link" className="text-zinc-400 p-0 h-auto">*/}
                    {/*        Contact us*/}
                    {/*      </Button>{' '}*/}
                    {/*      if you need more space*/}
                    {/*    </p>*/}
                    {/*    <div className="flex items-center gap-1 text-sm text-zinc-500">*/}
                    {/*      <span className="font-medium uppercase">Tip</span>*/}
                    {/*      Use <span className="italic">butler</span> to upload files: it only uploads what&apos;s changed, generates patches for the{' '}*/}
                    {/*      <Button variant="link" className="text-pink-500 p-0 h-auto">*/}
                    {/*        itch.io app*/}
                    {/*      </Button>*/}
                    {/*      , and you can automate it.{' '}*/}
                    {/*      <Button variant="link" className="text-pink-500 p-0 h-auto">*/}
                    {/*        Get started!*/}
                    {/*      </Button>*/}
                    {/*    </div>*/}
                    {/*  </div>*/}
                    {/*</div>*/}

                    <div>
                      <div className="flex items-center gap-2">
                        <Label className="text-zinc-400">Tags</Label>
                        <Button variant="link" className="text-zinc-400 p-0 h-auto text-sm">
                          Tips for choosing tags
                        </Button>
                      </div>
                      <p className="text-sm text-zinc-500 mt-1">
                        Any other keywords someone might search to find your game. Max of 10.
                        <br />
                        Avoid duplicating any platforms provided on files above.
                      </p>
                      <Input
                        placeholder="Click to view options, type to filter or enter custom tag"
                        className="mt-2 bg-zinc-900 border-zinc-800"
                        value={formData.tags}
                        onChange={(e) => setFormData((prev) => ({ ...prev, tags: e.target.value }))}
                      />
                    </div>

                    <div>
                      <div className="flex items-center gap-2">
                        <Label className="text-zinc-400">AI generation disclosure</Label>
                        <span className="text-[10px] font-medium bg-zinc-800 text-zinc-400 px-1.5 py-0.5 rounded">NEW</span>
                        <Button variant="link" className="text-zinc-400 p-0 h-auto text-sm">
                          Learn more <ExternalLink className="h-3 w-3 ml-1 inline" />
                        </Button>
                      </div>
                      <p className="text-sm text-zinc-500 mt-1">
                        Please disclose if this project contains content produced by generative AI tools such as LLMs, ChatGPT, Midjourney, Stable Diffusion,
                        etc., even if you hand-edited it.
                      </p>
                      <RadioGroup
                        value={formData.aiDisclosure}
                        onValueChange={(value) => setFormData((prev) => ({ ...prev, aiDisclosure: value }))}
                        className="mt-3 space-y-3"
                      >
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="yes" id="ai-yes" className="border-zinc-800" />
                          <Label htmlFor="ai-yes" className="text-zinc-400">
                            Yes — This project contains the output of Generative AI
                          </Label>
                        </div>
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="no" id="ai-no" className="border-zinc-800" />
                          <Label htmlFor="ai-no" className="text-zinc-400">
                            No — This project does not contain the output of Generative AI
                          </Label>
                        </div>
                      </RadioGroup>
                    </div>

                    <div>
                      <Label className="text-zinc-400">App store links</Label>
                      <p className="text-sm text-zinc-500 mt-1">If your project is available on any other stores we&apos;ll link to it.</p>
                      <div className="mt-3 flex flex-wrap gap-2">
                        <Button variant="outline" size="sm" className="bg-zinc-900 border-zinc-800 text-zinc-400">
                          <PlusIcon className="h-4 w-4 mr-2" />
                          Steam
                        </Button>
                        <Button variant="outline" size="sm" className="bg-zinc-900 border-zinc-800 text-zinc-400">
                          <PlusIcon className="h-4 w-4 mr-2" />
                          Apple App Store
                        </Button>
                        <Button variant="outline" size="sm" className="bg-zinc-900 border-zinc-800 text-zinc-400">
                          <PlusIcon className="h-4 w-4 mr-2" />
                          Google Play
                        </Button>
                        <Button variant="outline" size="sm" className="bg-zinc-900 border-zinc-800 text-zinc-400">
                          <PlusIcon className="h-4 w-4 mr-2" />
                          Amazon App Store
                        </Button>
                        <Button variant="outline" size="sm" className="bg-zinc-900 border-zinc-800 text-zinc-400">
                          <PlusIcon className="h-4 w-4 mr-2" />
                          Windows Store
                        </Button>
                      </div>
                    </div>

                    <div>
                      <div className="flex items-center gap-2">
                        <Label className="text-zinc-400">Custom noun</Label>
                        <Tooltip>
                          <TooltipTrigger>
                            <HelpCircle className="h-4 w-4 text-zinc-400" />
                          </TooltipTrigger>
                          <TooltipContent>
                            <p>You can change how itch.io refers to your project by providing a custom noun.</p>
                          </TooltipContent>
                        </Tooltip>
                      </div>
                      <p className="text-sm text-zinc-500 mt-1">Leave blank to default to: &apos;mod&apos;</p>
                      <Input
                        placeholder="Optional"
                        className="mt-2 bg-zinc-900 border-zinc-800"
                        value={formData.customNoun}
                        onChange={(e) => setFormData((prev) => ({ ...prev, customNoun: e.target.value }))}
                      />
                    </div>
                  </TabsContent>
                  <TabsContent value="details" className="space-y-6">
                    {/* Details Fields */}
                    <div>
                      <Label className="text-zinc-400">Details</Label>
                      <div className="mt-2 border border-zinc-800 rounded-lg">
                        <div className="flex items-center gap-1 p-2 border-b border-zinc-800">
                          <Button variant="ghost" size="sm" className="h-8 w-8 p-0 text-zinc-400">
                            <Code className="h-4 w-4" />
                          </Button>
                          <Button variant="ghost" size="sm" className="h-8 w-8 p-0 text-zinc-400">
                            <Bold className="h-4 w-4" />
                          </Button>
                          <Button variant="ghost" size="sm" className="h-8 w-8 p-0 text-zinc-400">
                            <Italic className="h-4 w-4" />
                          </Button>
                          <Button variant="ghost" size="sm" className="h-8 w-8 p-0 text-zinc-400">
                            <List className="h-4 w-4" />
                          </Button>
                          <Button variant="ghost" size="sm" className="h-8 w-8 p-0 text-zinc-400">
                            <ListOrdered className="h-4 w-4" />
                          </Button>
                          <Button variant="ghost" size="sm" className="h-8 w-8 p-0 text-zinc-400">
                            <Link2 className="h-4 w-4" />
                          </Button>
                          <Button variant="ghost" size="sm" className="h-8 w-8 p-0 text-zinc-400">
                            <ImageIcon className="h-4 w-4" />
                          </Button>
                        </div>
                        <Textarea
                          placeholder="This will make up the content of your game page."
                          className="border-0 rounded-none bg-transparent min-h-[200px]"
                          value={formData.description}
                          onChange={(e) => setFormData((prev) => ({ ...prev, description: e.target.value }))}
                        />
                      </div>
                    </div>
                  </TabsContent>
                  <TabsContent value="community" className="space-y-6">
                    {/* Community Fields */}
                    <div>
                      <Label className="text-zinc-400">Community</Label>
                      <p className="text-sm text-zinc-500 mt-1">Build a community for your project by letting people post to your page.</p>
                      <RadioGroup
                        value={formData.community}
                        onValueChange={(value) => setFormData((prev) => ({ ...prev, community: value }))}
                        className="mt-3 space-y-3"
                      >
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="disabled" id="community-disabled" className="border-zinc-800" />
                          <Label htmlFor="community-disabled" className="text-zinc-400">
                            Disabled
                          </Label>
                        </div>
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="comments" id="community-comments" className="border-zinc-800" />
                          <Label htmlFor="community-comments" className="text-zinc-400">
                            Comments — Add a nested comment thread to the bottom of the project page
                          </Label>
                        </div>
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="discussion" id="community-discussion" className="border-zinc-800" />
                          <Label htmlFor="community-discussion" className="text-zinc-400">
                            Discussion board — Add a dedicated community page with categories, threads, replies & more
                          </Label>
                        </div>
                      </RadioGroup>
                    </div>
                  </TabsContent>
                  <TabsContent value="visibility" className="space-y-6">
                    {/* Visibility Fields */}
                    <div>
                      <Label className="text-zinc-400">Visibility & access</Label>
                      <p className="text-sm text-zinc-500 mt-1">
                        Use Draft to review your page before making it public.{' '}
                        <Button variant="link" className="text-pink-500 p-0 h-auto">
                          Learn more about access modes
                        </Button>
                      </p>
                      <RadioGroup
                        value={formData.visibility}
                        onValueChange={(value) => setFormData((prev) => ({ ...prev, visibility: value }))}
                        className="mt-3 space-y-3"
                      >
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="draft" id="visibility-draft" className="border-zinc-800" />
                          <Label htmlFor="visibility-draft" className="text-zinc-400">
                            Draft — Only those who can edit the project can view the page
                          </Label>
                        </div>
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="restricted" id="visibility-restricted" className="border-zinc-800" />
                          <Label htmlFor="visibility-restricted" className="text-zinc-400">
                            Restricted — Only owners & authorized people can view the page
                          </Label>
                        </div>
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="public" id="visibility-public" className="border-zinc-800" />
                          <Label htmlFor="visibility-public" className="text-zinc-400">
                            Public — Anyone can view the page
                          </Label>
                        </div>
                      </RadioGroup>
                    </div>
                  </TabsContent>
                </Tabs>

                <div className="flex justify-end gap-4 pt-4 border-t border-zinc-800">
                  <Button type="button" variant="outline" onClick={() => setIsOpen(false)}>
                    Cancel
                  </Button>
                  <Button type="submit">Continue</Button>
                </div>
              </form>
            </div>
          </div>
        </TooltipProvider>
      </SheetContent>
    </Sheet>
  );
}
