import { buildActivitySentence } from './build-activity-sentence';
import { ActivityEntry } from '../work-items.service';

function entry(overrides: Partial<ActivityEntry> = {}): ActivityEntry {
  return {
    id: 1,
    workItemId: 10,
    workItemTitle: 'Fix login',
    workItemType: 'Task',
    eventType: 'Created',
    field: null,
    oldValue: null,
    newValue: null,
    actorUserId: 1,
    actorName: 'Jane',
    createdAt: '2026-07-22T09:00:00Z',
    ...overrides,
  };
}

describe('buildActivitySentence', () => {
  it('renders a Created entry', () => {
    expect(buildActivitySentence(entry())).toBe("Jane created Task 'Fix login'");
  });

  it('renders a Status FieldChanged entry', () => {
    const sentence = buildActivitySentence(
      entry({ eventType: 'FieldChanged', field: 'Status', oldValue: 'To Do', newValue: 'In Progress' })
    );
    expect(sentence).toBe("Jane changed Task 'Fix login' status from To Do to In Progress");
  });

  it('renders a Priority FieldChanged entry', () => {
    const sentence = buildActivitySentence(
      entry({ eventType: 'FieldChanged', field: 'Priority', oldValue: 'Medium', newValue: 'High' })
    );
    expect(sentence).toBe("Jane changed Task 'Fix login' priority from Medium to High");
  });

  it('renders an Assignee FieldChanged entry', () => {
    const sentence = buildActivitySentence(
      entry({ eventType: 'FieldChanged', field: 'Assignee', oldValue: 'Unassigned', newValue: 'Sam Lee' })
    );
    expect(sentence).toBe("Jane changed Task 'Fix login' assignee from Unassigned to Sam Lee");
  });

  it('renders a Sprint FieldChanged entry, including the null-to-"Backlog" display case', () => {
    const sentence = buildActivitySentence(
      entry({ eventType: 'FieldChanged', field: 'Sprint', oldValue: 'Backlog', newValue: 'Sprint 1' })
    );
    expect(sentence).toBe("Jane changed Task 'Fix login' sprint from Backlog to Sprint 1");

    const removed = buildActivitySentence(
      entry({ eventType: 'FieldChanged', field: 'Sprint', oldValue: 'Sprint 1', newValue: 'Backlog' })
    );
    expect(removed).toBe("Jane changed Task 'Fix login' sprint from Sprint 1 to Backlog");
  });
});
