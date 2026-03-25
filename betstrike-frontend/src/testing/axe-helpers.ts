import * as axe from 'axe-core';

const axeOptions: axe.RunOptions = {
  rules: {
    // Disabled in jsdom because layout APIs are incomplete and produce false positives.
    'color-contrast': { enabled: false }
  }
};

function formatAxeViolations(violations: ReadonlyArray<axe.Result>): string {
  return violations
    .map((violation) => {
      const nodes = violation.nodes.map((node) => node.html.trim()).join('\n');
      return `${violation.id}: ${violation.help}\n${nodes}`;
    })
    .join('\n\n');
}

export async function expectNoAxeViolations(element: Element): Promise<void> {
  const results = await axe.run(element, axeOptions);

  if (results.violations.length > 0) {
    throw new Error(`AXE violations found:\n${formatAxeViolations(results.violations)}`);
  }
}

