import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { ProjectsListComponent } from './projects-list.component';
import { ProjectsService } from '../projects.service';
import { AuthService } from '../../auth/auth.service';

const sampleProjects = [
  { id: 1, name: 'Website Redesign', createdByName: 'Ada Lovelace', createdAt: '2026-01-01T00:00:00Z', openWorkItemCount: 3 },
  { id: 2, name: 'Mobile App', createdByName: 'Grace Hopper', createdAt: '2026-01-02T00:00:00Z', openWorkItemCount: 0 },
];

function configure(
  getProjects = vi.fn().mockResolvedValue({ items: sampleProjects, page: 1, pageSize: 20, totalCount: 2 }),
  role: string | null = null
) {
  TestBed.configureTestingModule({
    imports: [ProjectsListComponent],
    providers: [
      provideRouter([]),
      { provide: ProjectsService, useValue: { getProjects } },
      { provide: AuthService, useValue: { currentRole: () => role } },
    ],
  });
  return { getProjects };
}

async function render() {
  const fixture = TestBed.createComponent(ProjectsListComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

describe('ProjectsListComponent', () => {
  it('renders the paginated list of projects with open-item counts', async () => {
    configure();
    const fixture = await render();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Website Redesign');
    expect(text).toContain('Ada Lovelace');
    expect(text).toContain('3');
    expect(text).toContain('Mobile App');
  });

  // Feature 005 Polish: Board is now the default project view, so opening a project
  // from this list should land there directly rather than on List.
  it('links each project to its Board view by default', async () => {
    configure();
    const fixture = await render();

    const link = fixture.nativeElement.querySelector('td a') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/projects/1?view=board');
  });

  it('renders the created date in friendly format, never raw ISO (SC-006)', async () => {
    configure();
    const fixture = await render();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Jan 1, 2026');
    expect(text).not.toMatch(/\d{4}-\d{2}-\d{2}T/);
  });

  it('shows an Edit link per project for a Manager', async () => {
    configure(undefined, 'Manager');
    const fixture = await render();

    expect(fixture.nativeElement.querySelectorAll('.project-edit-link').length).toBe(2);
  });

  it('shows an Edit link per project for an Admin', async () => {
    configure(undefined, 'Admin');
    const fixture = await render();

    expect(fixture.nativeElement.querySelectorAll('.project-edit-link').length).toBe(2);
  });

  it('hides the Edit link for a Developer', async () => {
    configure(undefined, 'Developer');
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.project-edit-link')).toBeNull();
  });
});
