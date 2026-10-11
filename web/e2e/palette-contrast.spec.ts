import { expect, test } from '@playwright/test';

const paletteTokens = [
  '--ui-color-foreground',
  '--ui-color-foreground-strong',
  '--ui-color-foreground-soft',
  '--ui-color-muted',
  '--ui-color-primary',
  '--ui-color-primary-hover',
  '--ui-color-primary-foreground',
  '--ui-color-accent',
  '--ui-color-sidebar',
  '--ui-color-background',
  '--ui-color-surface',
  '--ui-color-surface-muted',
  '--ui-color-border',
  '--ui-color-destructive',
  '--ui-color-destructive-background',
  '--ui-color-status-active',
  '--ui-color-status-active-background',
  '--ui-color-status-warning',
  '--ui-color-status-warning-background',
  '--ui-color-focus',
  '--ui-button-primary-background',
  '--ui-button-primary-foreground',
  '--ui-button-primary-hover-background',
  '--ui-button-quiet-background',
  '--ui-button-quiet-foreground',
  '--ui-button-quiet-border',
  '--ui-button-text-foreground',
  '--ui-focus-ring',
  '--ui-status-active-foreground',
  '--ui-status-active-background',
  '--ui-status-warning-foreground',
  '--ui-status-warning-background',
];

function luminance(color: string): number {
  const hex = color.match(/^#([\da-f]{3}|[\da-f]{6})$/i)?.[1];
  const srgb = color.match(/^color\(srgb\s+([^)]+)\)$/i)?.[1];
  const rgbFunction = color.match(/^rgba?\(([^)]+)\)$/i)?.[1];
  const channels = hex
    ? hex.length === 3
      ? [...hex].map(channel => Number.parseInt(channel.repeat(2), 16))
      : [0, 2, 4].map(index => Number.parseInt(hex.slice(index, index + 2), 16))
    : (srgb ?? rgbFunction)
        ?.split(/[,/\s]+/)
        .filter(Boolean)
        .slice(0, 3)
        .map(channel => Number.parseFloat(channel));

  if (!channels || channels.length !== 3 || channels.some(channel => !Number.isFinite(channel))) {
    throw new Error(`Unsupported computed CSS color: ${color}`);
  }

  const [red, green, blue] = channels.map(channel => {
    const normalized = srgb ? channel : channel / 255;
    return normalized <= 0.04045 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
  });

  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

function contrastRatio(foreground: string, background: string): number {
  const foregroundLuminance = luminance(foreground);
  const backgroundLuminance = luminance(background);
  const lighter = Math.max(foregroundLuminance, backgroundLuminance);
  const darker = Math.min(foregroundLuminance, backgroundLuminance);
  return (lighter + 0.05) / (darker + 0.05);
}

test('semantic text, status, and control pairs meet contrast thresholds', async ({ page }) => {
  await page.goto('/login');

  const colors = await page.evaluate(tokens => {
    const styles = getComputedStyle(document.documentElement);
    const probe = document.createElement('span');
    document.body.append(probe);
    const colors = Object.fromEntries(
      tokens.map(token => {
        probe.style.color = `var(${token})`;
        return [
          token,
          {
            raw: styles.getPropertyValue(token).trim(),
            computed: getComputedStyle(probe).color,
          },
        ];
      }),
    );
    probe.remove();
    return colors;
  }, paletteTokens);

  for (const token of paletteTokens) {
    expect(colors[token].raw, `${token} must be defined in the rendered stylesheet`).not.toBe('');
  }

  const textPairs = [
    ['default text on page background', '--ui-color-foreground', '--ui-color-background'],
    ['strong text on surface', '--ui-color-foreground-strong', '--ui-color-surface'],
    ['supporting text on surface', '--ui-color-foreground-soft', '--ui-color-surface'],
    ['muted text on surface', '--ui-color-muted', '--ui-color-surface'],
    ['accent text on surface', '--ui-color-accent', '--ui-color-surface'],
    ['primary action text', '--ui-button-primary-foreground', '--ui-button-primary-background'],
    [
      'primary action hover text',
      '--ui-button-primary-foreground',
      '--ui-button-primary-hover-background',
    ],
    ['quiet button text', '--ui-button-quiet-foreground', '--ui-button-quiet-background'],
    ['text button text', '--ui-button-text-foreground', '--ui-button-quiet-background'],
    ['sidebar text', '--ui-color-primary-foreground', '--ui-color-sidebar'],
    ['active status text', '--ui-status-active-foreground', '--ui-status-active-background'],
    ['warning status text', '--ui-status-warning-foreground', '--ui-status-warning-background'],
    ['destructive/error text', '--ui-color-destructive', '--ui-color-destructive-background'],
  ] as const;

  for (const [name, foreground, background] of textPairs) {
    expect(
      contrastRatio(colors[foreground].computed, colors[background].computed),
      `${name} must meet the 4.5:1 normal-text contrast threshold`,
    ).toBeGreaterThanOrEqual(4.5);
  }

  expect(
    contrastRatio(
      colors['--ui-color-foreground-strong'].computed,
      colors['--ui-color-background'].computed,
    ),
    'large headings must meet the 3:1 large-text contrast threshold',
  ).toBeGreaterThanOrEqual(3);

  const controlPairs = [
    ['control border on surface', '--ui-button-quiet-border', '--ui-button-quiet-background'],
    ['focus indicator on surface', '--ui-focus-ring', '--ui-color-surface'],
    ['focus indicator on muted surface', '--ui-focus-ring', '--ui-color-surface-muted'],
    ['primary control boundary on surface', '--ui-button-primary-background', '--ui-color-surface'],
  ] as const;

  for (const [name, foreground, background] of controlPairs) {
    expect(
      contrastRatio(colors[foreground].computed, colors[background].computed),
      `${name} must meet the 3:1 non-text contrast threshold`,
    ).toBeGreaterThanOrEqual(3);
  }
});
