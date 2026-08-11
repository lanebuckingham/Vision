---
inclusion: fileMatch
fileMatchPattern:
  - "**/*.ts"
  - "**/*.tsx"
  - "**/*.css"
---

# Vision Frontend Standards

## Product Experience

The frontend is the employer-facing surface of Vision.

Prioritize:

- Fast rendering
- Clear hierarchy
- Professional visual polish
- Responsive behavior
- Accessible interactions
- Predictable navigation
- Useful loading/error states

## Stack

Use:

- React
- TypeScript
- Next.js

Do not introduce another frontend framework.

## TypeScript

Prefer strong typing.

Avoid `any` unless integration constraints genuinely require it and the reason is clear.

Model API contracts explicitly.

## Components

Create reusable components when reuse or consistency justifies them.

Do not over-componentize trivial markup.

Keep domain/page orchestration separate from purely presentational components where useful.

## Data Fetching

Avoid unnecessary sequential requests.

Design dashboard data fetching to support fast first paint.

Handle:

- Loading
- Empty state
- Error state
- Successful state

Do not leave users looking at indefinite spinners.

## Authorization UX

Hide or disable controls the current role cannot use where appropriate.

Remember: API authorization remains authoritative.

## Demo Quality

The primary dashboard must make the product understandable quickly.

Use realistic seeded data and meaningful labels.

Do not display lorem ipsum, fake placeholder metrics, or obviously unfinished UI in the production demo.

## Responsive Design

Vision should work well on desktop and remain usable on tablets/mobile screens.

The MVP does not require a native mobile application.

## Accessibility

Use semantic HTML.

Support keyboard navigation for important workflows.

Use labels for form fields and accessible names for controls.

Do not rely only on color to communicate status.

## Performance

Avoid unnecessary large dependencies.

Keep client-side JavaScript reasonable.

Use Next.js capabilities intentionally without moving authoritative business logic into the frontend.
