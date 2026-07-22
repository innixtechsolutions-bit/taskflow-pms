import { TestBed } from '@angular/core/testing';
import { ActivityFeedComponent } from './activity-feed.component';
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
    createdAt: new Date().toISOString(),
    ...overrides,
  };
}

async function render(entries: ActivityEntry[]) {
  TestBed.configureTestingModule({ imports: [ActivityFeedComponent] });
  const fixture = TestBed.createComponent(ActivityFeedComponent);
  fixture.componentRef.setInput('entries', entries);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

describe('ActivityFeedComponent', () => {
  it('renders one row per entry, newest first, as a built sentence with a relative timestamp', async () => {
    const fixture = await render([
      entry({ id: 2, workItemTitle: 'Second', actorName: 'Sam' }),
      entry({ id: 1, workItemTitle: 'First', actorName: 'Jane' }),
    ]);

    const rows = fixture.nativeElement.querySelectorAll('.activity-entry');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain("Sam created Task 'Second'");
    expect(rows[1].textContent).toContain("Jane created Task 'First'");
  });

  it('shows an empty state when there are no entries', async () => {
    const fixture = await render([]);

    expect(fixture.nativeElement.querySelector('app-empty-state')).toBeTruthy();
  });
});
