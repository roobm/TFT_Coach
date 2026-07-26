# Feature: Patch Notes page + hamburger navigation

## Goal
Add a useful in-app Patch Notes page and replace the top route buttons with a mobile-first, accessible hamburger navigation.

## Ordered task list
1. [x] Review current route/layout/CSS structure in `frontend/src`.
2. [x] Add new `PatchNotesPage` component with official TFT links and clear CTA to the patch notes hub.
3. [x] Register a `/patch-notes` route while preserving existing routes (`/`, `/history`, `/comps`, `/live-coach`).
4. [x] Replace header nav buttons with a hamburger toggle + panel, including active-route styles and aria attributes.
5. [x] Add/adjust CSS for hamburger menu and patch notes panels in the existing dark theme.
6. [x] Run frontend build and verify no type/build errors.

## Status
- Complete.
