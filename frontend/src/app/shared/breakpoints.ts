/**
 * Tablet breakpoint used by the app shell's sidebar collapse (CDK
 * `BreakpointObserver`). Mirrors `$breakpoint-tablet-px` in
 * `design-tokens.scss` — CSS custom properties can't be read inside a
 * `@media` condition, so the pixel value is duplicated here on purpose;
 * keep both in sync if it ever changes.
 */
export const TABLET_BREAKPOINT_PX = 1024;

export const TABLET_BREAKPOINT_QUERY = `(max-width: ${TABLET_BREAKPOINT_PX}px)`;
