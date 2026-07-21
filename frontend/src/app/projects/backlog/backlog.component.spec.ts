import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { vi } from 'vitest';
import { BacklogComponent } from './backlog.component';
import { Sprint, SprintsService } from '../sprints.service';
import { AuthService } from '../../auth/auth.service';

function sampleSprints(): Sprint[] {
  return [
    { id: 1, projectId: 1, name: 'Sprint 1', startDate: '2026-08-01', endDate: '2026-08-15', status: 'Planned', itemCount: 2 },
    { id: 2, projectId: 1, name: 'Sprint 2', startDate: '2026-08-16', endDate: '2026-08-30', status: 'Planned', itemCount: 0 },
  ];
}

function configure(
  getSprints = vi.fn().mockResolvedValue(sampleSprints()),
  role: 'Developer' | 'Manager' | 'Admin' | null = 'Developer'
) {
  const dialogOpen = vi.fn().mockReturnValue({});
  TestBed.configureTestingModule({
    imports: [BacklogComponent],
    providers: [
      { provide: SprintsService, useValue: { getSprints } },
      { provide: AuthService, useValue: { currentRole: () => role } },
      { provide: MatDialog, useValue: { open: dialogOpen } },
    ],
  });
  return { getSprints, dialogOpen };
}

async function render(projectId = 1) {
  const fixture = TestBed.createComponent(BacklogComponent);
  fixture.componentRef.setInput('projectId', projectId);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

describe('BacklogComponent', () => {
  it('renders each sprint (name, dates, status, item count) from getSprints, soonest-first order preserved', async () => {
    configure();
    const fixture = await render();

    const sections = fixture.nativeElement.querySelectorAll('.sprint-section');
    expect(sections.length).toBe(2);
    expect(sections[0].textContent).toContain('Sprint 1');
    expect(sections[0].textContent).toContain('2 work item(s)');
    expect(sections[1].textContent).toContain('Sprint 2');
  });

  it('shows an empty state with no sprint sections when the project has none', async () => {
    configure(vi.fn().mockResolvedValue([]));
    const fixture = await render();

    expect(fixture.nativeElement.querySelectorAll('.sprint-section').length).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('No sprints yet');
  });

  it('shows "Create sprint" for a Manager/Admin and opens SprintFormComponent on click', async () => {
    const { dialogOpen } = configure(undefined, 'Manager');
    const fixture = await render();

    const button = fixture.nativeElement.querySelector('.create-sprint-button') as HTMLButtonElement;
    expect(button).toBeTruthy();
    button.click();

    expect(dialogOpen).toHaveBeenCalled();
  });

  it('hides "Create sprint" for a Developer', async () => {
    configure(undefined, 'Developer');
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.create-sprint-button')).toBeNull();
  });

  it('refreshes the sprint list after a sprint is created', async () => {
    const getSprints = vi.fn().mockResolvedValueOnce([]).mockResolvedValueOnce(sampleSprints());
    const dialogOpen = vi.fn((_component: unknown, config: { data: { onSaved: () => void } }) => {
      config.data.onSaved();
      return {};
    });
    TestBed.configureTestingModule({
      imports: [BacklogComponent],
      providers: [
        { provide: SprintsService, useValue: { getSprints } },
        { provide: AuthService, useValue: { currentRole: () => 'Manager' } },
        { provide: MatDialog, useValue: { open: dialogOpen } },
      ],
    });
    const fixture = await render();
    expect(fixture.nativeElement.querySelectorAll('.sprint-section').length).toBe(0);

    (fixture.nativeElement.querySelector('.create-sprint-button') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(getSprints).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.querySelectorAll('.sprint-section').length).toBe(2);
  });
});
