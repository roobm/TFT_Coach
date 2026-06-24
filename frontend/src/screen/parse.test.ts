import { describe, expect, it } from 'vitest';
import { parseRead, parseReads, parseStage, MIN_CONFIDENCE } from './parse';

const HIGH = MIN_CONFIDENCE + 20;

describe('parseStage', () => {
  it('parses a clean stage', () => {
    expect(parseStage('3-2')).toBe('3-2');
  });

  it('tolerates spaces and em/en dashes', () => {
    expect(parseStage('3 — 2')).toBe('3-2');
    expect(parseStage('4 - 6')).toBe('4-6');
  });

  it('rejects out-of-range or malformed stages', () => {
    expect(parseStage('0-2')).toBeNull();
    expect(parseStage('garbage')).toBeNull();
  });
});

describe('parseRead', () => {
  it('accepts a plausible gold read', () => {
    expect(parseRead({ field: 'gold', text: '52', confidence: HIGH })).toEqual({ gold: 52 });
  });

  it('strips noise around the number', () => {
    expect(parseRead({ field: 'gold', text: ' 4 8 ', confidence: HIGH })).toEqual({ gold: 4 });
  });

  it('rejects low-confidence reads', () => {
    expect(parseRead({ field: 'gold', text: '50', confidence: MIN_CONFIDENCE - 1 })).toEqual({});
  });

  it('rejects out-of-range level', () => {
    expect(parseRead({ field: 'level', text: '14', confidence: HIGH })).toEqual({});
    expect(parseRead({ field: 'level', text: '8', confidence: HIGH })).toEqual({ level: 8 });
  });

  it('clamps health to 0..100', () => {
    expect(parseRead({ field: 'health', text: '100', confidence: HIGH })).toEqual({ health: 100 });
    expect(parseRead({ field: 'health', text: '250', confidence: HIGH })).toEqual({});
  });

  it('parses a stage read', () => {
    expect(parseRead({ field: 'stage', text: '4-1', confidence: HIGH })).toEqual({ stage: '4-1' });
  });

  it('returns empty for a stage that fails to parse', () => {
    expect(parseRead({ field: 'stage', text: '??', confidence: HIGH })).toEqual({});
  });
});

describe('parseReads', () => {
  it('merges valid reads and drops invalid ones', () => {
    const result = parseReads([
      { field: 'gold', text: '30', confidence: HIGH },
      { field: 'level', text: '7', confidence: HIGH },
      { field: 'stage', text: '3-5', confidence: HIGH },
      { field: 'health', text: 'xx', confidence: HIGH },
    ]);
    expect(result).toEqual({ gold: 30, level: 7, stage: '3-5' });
  });
});
