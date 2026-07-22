import { TestBed } from '@angular/core/testing';
import { PriorityBarChartComponent } from './priority-bar-chart.component';
import { PriorityBreakdownItem } from '../work-items.service';

function sampleBreakdown(): PriorityBreakdownItem[] {
  return [
    { priority: 'Low', count: 2 },
    { priority: 'Medium', count: 5 },
    { priority: 'High', count: 1 },
    { priority: 'Critical', count: 0 },
  ];
}

async function render(breakdown: PriorityBreakdownItem[]) {
  TestBed.configureTestingModule({ imports: [PriorityBarChartComponent] });
  const fixture = TestBed.createComponent(PriorityBarChartComponent);
  fixture.componentRef.setInput('breakdown', breakdown);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

describe('PriorityBarChartComponent', () => {
  it('always renders all 4 priority levels, including one at zero count', async () => {
    const fixture = await render(sampleBreakdown());

    const rows = fixture.nativeElement.querySelectorAll('.priority-bar-row');
    expect(rows.length).toBe(4);
    expect(rows[3].textContent).toContain('Critical');
    expect(rows[3].textContent).toContain('0');
  });

  it('shows the correct count per level', async () => {
    const fixture = await render(sampleBreakdown());

    const rows = fixture.nativeElement.querySelectorAll('.priority-bar-row');
    expect(rows[1].textContent).toContain('Medium');
    expect(rows[1].textContent).toContain('5');
  });
});
