import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { ProjectsListComponent } from './projects-list.component';
import { ProjectsService } from '../projects.service';

const sampleProjects = [
  { id: 1, name: 'Website Redesign', createdByName: 'Ada Lovelace', createdAt: '2026-01-01T00:00:00Z', openWorkItemCount: 3 },
  { id: 2, name: 'Mobile App', createdByName: 'Grace Hopper', createdAt: '2026-01-02T00:00:00Z', openWorkItemCount: 0 },
];

function configure(getProjects = vi.fn().mockResolvedValue({ items: sampleProjects, page: 1, pageSize: 20, totalCount: 2 })) {
  TestBed.configureTestingModule({
    imports: [ProjectsListComponent],
    providers: [provideRouter([]), { provide: ProjectsService, useValue: { getProjects } }],
  });
  return { getProjects };
}

describe('ProjectsListComponent', () => {
  it('renders the paginated list of projects with open-item counts', async () => {
    configure();
    const fixture = TestBed.createComponent(ProjectsListComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Website Redesign');
    expect(text).toContain('Ada Lovelace');
    expect(text).toContain('3');
    expect(text).toContain('Mobile App');
  });
});
