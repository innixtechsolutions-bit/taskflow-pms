import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { ProjectDetailComponent } from './project-detail.component';
import { ProjectsService } from '../projects.service';

const sampleProject = {
  id: 1,
  name: 'Website Redesign',
  description: 'Rebuild the marketing site',
  createdByName: 'Ada Lovelace',
  createdAt: '2026-01-01T00:00:00Z',
  totalWorkItemCount: 0,
};

function configure(getProject = vi.fn().mockResolvedValue(sampleProject)) {
  TestBed.configureTestingModule({
    imports: [ProjectDetailComponent],
    providers: [
      provideRouter([]),
      { provide: ProjectsService, useValue: { getProject } },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } } },
    ],
  });
  return { getProject };
}

describe('ProjectDetailComponent', () => {
  it("renders the project's header info", async () => {
    configure();
    const fixture = TestBed.createComponent(ProjectDetailComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Website Redesign');
    expect(text).toContain('Rebuild the marketing site');
    expect(text).toContain('Ada Lovelace');
  });

  it('shows "No work items yet" when the project has none', async () => {
    configure();
    const fixture = TestBed.createComponent(ProjectDetailComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No work items yet');
  });
});
