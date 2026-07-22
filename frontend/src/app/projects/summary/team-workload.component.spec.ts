import { TestBed } from '@angular/core/testing';
import { TeamWorkloadComponent } from './team-workload.component';
import { WorkloadRow } from '../work-items.service';

async function render(workload: WorkloadRow[]) {
  TestBed.configureTestingModule({ imports: [TeamWorkloadComponent] });
  const fixture = TestBed.createComponent(TeamWorkloadComponent);
  fixture.componentRef.setInput('workload', workload);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

describe('TeamWorkloadComponent', () => {
  it('renders one row per entry, in the given (count-descending) order', async () => {
    const fixture = await render([
      { userId: 1, displayName: 'Jane Doe', openItemCount: 3 },
      { userId: 2, displayName: 'Sam Lee', openItemCount: 1 },
      { userId: null, displayName: 'Unassigned', openItemCount: 1 },
    ]);

    const rows = fixture.nativeElement.querySelectorAll('.workload-row');
    expect(rows.length).toBe(3);
    expect(rows[0].textContent).toContain('Jane Doe');
    expect(rows[0].textContent).toContain('3');
    expect(rows[2].textContent).toContain('Unassigned');
  });

  it('renders a zero-load Manager/Admin row when present', async () => {
    const fixture = await render([
      { userId: 1, displayName: 'Jane Doe', openItemCount: 3 },
      { userId: 5, displayName: 'Pat Manager', openItemCount: 0 },
    ]);

    const rows = fixture.nativeElement.querySelectorAll('.workload-row');
    expect(rows.length).toBe(2);
    expect(rows[1].textContent).toContain('Pat Manager');
    expect(rows[1].textContent).toContain('0');
  });

  it('renders no Unassigned row when none was supplied', async () => {
    const fixture = await render([{ userId: 1, displayName: 'Jane Doe', openItemCount: 3 }]);

    expect(fixture.nativeElement.textContent).not.toContain('Unassigned');
  });

  it('shows an empty state when there is no workload data', async () => {
    const fixture = await render([]);

    expect(fixture.nativeElement.querySelector('app-empty-state')).toBeTruthy();
  });
});
