import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { WorkItemFormComponent } from './work-item-form.component';
import { WorkItemsService } from '../work-items.service';

const sampleUsers = [
  { id: 1, fullName: 'Ada Lovelace' },
  { id: 2, fullName: 'Grace Hopper' },
];

function setInputValue(el: HTMLInputElement | HTMLTextAreaElement, value: string): void {
  el.value = value;
  el.dispatchEvent(new Event('input'));
}

function configure(
  createWorkItem = vi.fn(),
  getAssignableUsers = vi.fn().mockResolvedValue(sampleUsers)
) {
  TestBed.configureTestingModule({
    imports: [WorkItemFormComponent],
    providers: [
      provideRouter([]),
      { provide: WorkItemsService, useValue: { createWorkItem, getAssignableUsers } },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ projectId: '1' }) } } },
    ],
  });
  return { createWorkItem, getAssignableUsers };
}

describe('WorkItemFormComponent (create mode)', () => {
  it('submits with the title and the shown defaults', async () => {
    const { createWorkItem } = configure(vi.fn().mockResolvedValue({ id: 5 }));
    const fixture = TestBed.createComponent(WorkItemFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    setInputValue(root.querySelector<HTMLInputElement>('#title')!, 'Fix the login bug');
    root.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();

    expect(createWorkItem).toHaveBeenCalledWith(
      1,
      expect.objectContaining({ title: 'Fix the login bug', type: 'Task', priority: 'Medium', status: 'ToDo' })
    );
  });

  it("correctly pre-selects each dropdown's bound value on initial render, not just the first option", async () => {
    configure();
    const fixture = TestBed.createComponent(WorkItemFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    // 'Task' and 'Medium' are each the 3rd/2nd <option>, not the 1st — this only
    // passes if the dropdown is genuinely reading the bound value (research.md §6),
    // not silently defaulting to whichever <option> happens to come first.
    expect((root.querySelector('#type') as HTMLSelectElement).value).toBe('Task');
    expect((root.querySelector('#priority') as HTMLSelectElement).value).toBe('Medium');
    expect((root.querySelector('#status') as HTMLSelectElement).value).toBe('ToDo');
  });

  it('lists assignable users fetched from the server', async () => {
    configure();
    const fixture = TestBed.createComponent(WorkItemFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ada Lovelace');
    expect(fixture.nativeElement.textContent).toContain('Grace Hopper');
  });
});
