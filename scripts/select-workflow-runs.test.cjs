'use strict';

const assert = require('node:assert/strict');
const test = require('node:test');
const { selectRunsForDeletion } = require('./select-workflow-runs.cjs');

const now = Date.parse('2026-11-12T12:00:00Z');
const day = 24 * 60 * 60 * 1000;

function run(id, workflowId, ageDays, status = 'completed') {
  return {
    id,
    workflow_id: workflowId,
    created_at: new Date(now - ageDays * day).toISOString(),
    status,
  };
}

test('keeps the newest ten per workflow and deletes only completed runs older than 30 days', () => {
  const runs = [
    ...Array.from({ length: 10 }, (_, i) => run(i + 1, 1, 31 + i)),
    run(11, 1, 50),
    run(12, 1, 51, 'in_progress'),
    run(13, 1, 30),
    run(14, 1, 29),
    ...Array.from({ length: 10 }, (_, i) => run(i + 20, 2, 60 + i)),
    run(30, 2, 80),
  ].reverse();

  assert.deepEqual(selectRunsForDeletion(runs, now).map((item) => item.id), [9, 10, 11, 30]);
});

test('uses the run id to break creation-time ties and preserves non-completed runs', () => {
  const runs = Array.from({ length: 12 }, (_, i) => run(i + 1, 1, 40));
  runs[0].status = 'queued';
  assert.deepEqual(selectRunsForDeletion(runs, now).map((item) => item.id), [2]);
});

test('excludes the exact 30-day boundary even when outside the newest ten', () => {
  const runs = [
    ...Array.from({ length: 10 }, (_, i) => run(i + 1, 1, i)),
    run(11, 1, 30),
    { ...run(12, 1, 30), created_at: new Date(now - 30 * day - 1).toISOString() },
  ];
  assert.deepEqual(selectRunsForDeletion(runs, now).map((item) => item.id), [12]);
});

test('fails rather than deleting with malformed run data or current time', () => {
  assert.throws(() => selectRunsForDeletion([run(1, 1, 1), { ...run(2, 1, 2), created_at: 'invalid' }], now));
  assert.throws(() => selectRunsForDeletion([{ ...run(1, 1, 1), workflow_id: null }], now));
  assert.throws(() => selectRunsForDeletion([], NaN));
});
