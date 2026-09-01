# Social Feed Shell Design QA

## Comparison target

- Source visual truth: `D:\Codex\core\generated_images\01a03934-bd1f-7ab3-befd-3db1258c4cea\exec-6d5ef56d-7c94-4c34-bdb6-1461b8406505.png`
- Browser-rendered implementation: `C:\Users\MatheusMartins\AppData\Local\Temp\gameguild-social-shell-v3.png`
- Full side-by-side comparison: `C:\Users\MatheusMartins\AppData\Local\Temp\gameguild-social-shell-comparison.png`
- Focused sidebar comparison: `C:\Users\MatheusMartins\AppData\Local\Temp\gameguild-social-shell-left-comparison.png`
- Focused right-rail comparison: `C:\Users\MatheusMartins\AppData\Local\Temp\gameguild-social-shell-rail-comparison.png`
- Responsive implementation evidence: `C:\Users\MatheusMartins\AppData\Local\Temp\gameguild-social-shell-v3-mobile.png`

## Viewport, normalization, and state

- Source pixels: 1672 x 941.
- Implementation pixels: 1672 x 941.
- Desktop CSS viewport: 1672 x 941 at `deviceScaleFactor: 1`.
- Mobile CSS viewport and pixels: 390 x 844 at `deviceScaleFactor: 1`.
- Density normalization: none required; source and desktop implementation have equal pixel dimensions and density.
- State: authenticated local `admin` session, dark theme, `/en-US/social`, deterministic social demo content enabled for visual review.
- Intentional product override: the implementation keeps GameGuild's existing public website header above the social shell, as requested, instead of duplicating the reference's brand and account controls inside the sidebar.

## Findings

- No actionable P0, P1, or P2 mismatch remains for the requested shell scope.
- [P3] Live avatar and playtest artwork fidelity
  - Location: story row and right rail.
  - Evidence: the reference uses portrait and game-cover imagery; the implementation uses the product's initials fallback wherever the local API does not provide an image.
  - Impact: the shell remains clear and usable, but a populated production dataset will feel richer.
  - Follow-up: render uploaded avatar and cover URLs when those fields are available; retain initials only as the error fallback.

## Required fidelity surfaces

- Fonts and typography: the existing GameGuild sans stack is preserved. Weight, compact UI sizing, line height, uppercase community eyebrow, and muted metadata reproduce the hierarchy of the source without introducing a second type system.
- Spacing and layout rhythm: the implementation keeps the reference's left navigation, dominant central feed, and compact right rail. Card gaps, borders, 12px radii, sticky navigation, and dense metadata align with the source. The public 64px header is the requested intentional vertical offset.
- Colors and visual tokens: the background remains near-black navy with slate borders, cyan active/navigation accents, violet secondary accents, and green playtest actions. This also preserves the existing GameGuild public-header palette.
- Image quality and asset fidelity: the main feed uses a real high-resolution media asset with a wide responsive crop. Initials are used only as the product's data fallback; no handcrafted decorative SVG or CSS illustration replaces the source content.
- Copy and content: navigation, creator metadata, playtest summaries, calls to action, tags, and composer copy are realistic and aligned with GameGuild terminology.

## Full-view comparison evidence

- The combined image confirms the same three-zone information architecture and density: primary navigation, feed, and discovery rail.
- The authenticated dashboard header and dashboard search are absent.
- The exact public GameGuild header now owns brand, global navigation, GitHub, and account identity.
- The sidebar no longer repeats the signed-in user/team block at the bottom.
- The central feed keeps stories, composer, creator metadata, game/build metadata, media, engagement, and playtest actions above the fold.

## Focused comparison evidence

- Sidebar crop: active state, icon rhythm, Messages badge, divider, and Create hierarchy closely follow the reference. The removed lower account block is visible as an intentional difference.
- Right-rail crop: profile metrics, upcoming playtests, suggested creators, and trending tags preserve the same hierarchy and card density. Initials replace unavailable local imagery without changing layout.

## Comparison history

1. Initial implementation evidence: `C:\Users\MatheusMartins\AppData\Local\Temp\gameguild-social-shell-v2.png`.
   - [P2] Story row was sparse because live empty responses took precedence over demo content.
   - [P2] The feed image was too tall and displaced engagement content below the fold.
   - [P2] The right-rail profile used the generic local account instead of the reference's featured creator hierarchy.
2. Fixes applied:
   - Used deterministic demo stories and creators in local social demo mode.
   - Added the featured Marina Costa creator and a fuller story row.
   - Changed desktop media framing to a wider 16:7 crop.
   - Kept the right rail ordered as profile, playtests, creators, and trending tags.
3. Post-fix evidence: `C:\Users\MatheusMartins\AppData\Local\Temp\gameguild-social-shell-v3.png` and the focused comparison images above.
   - No P0, P1, or P2 issue remains.

## Interaction and responsive validation

- Authenticated route loaded successfully.
- Sidebar Create action expanded and focused the post composer (`composerBefore: 0`, `composerAfter: 1`).
- Desktop width matched the viewport (`scrollWidth: 1672`, `clientWidth: 1672`).
- Mobile width matched the viewport (`scrollWidth: 390`, `clientWidth: 390`); the desktop sidebar and right rail collapse cleanly.
- The dashboard search was absent and the sidebar contained no repeated account block.
- Browser console and page error collection returned no errors on desktop or mobile.

## Implementation checklist

- [x] Reuse the existing public GameGuild header.
- [x] Isolate `/social` from the authenticated dashboard shell.
- [x] Build the modern social navigation without a lower account duplicate.
- [x] Preserve the reference's three-column feed hierarchy.
- [x] Make the Create action open the real composer.
- [x] Verify desktop and mobile without horizontal overflow or console errors.

final result: passed
