import { TestBed } from '@angular/core/testing';
import { StatusDonutChartComponent } from './status-donut-chart.component';
import { StatusBreakdownItem } from '../work-items.service';

function sampleBreakdown(): StatusBreakdownItem[] {
  return [
    { statusId: 1, name: 'To Do', colorKey: 'Slate', count: 3 },
    { statusId: 2, name: 'In Progress', colorKey: 'Blue', count: 2 },
    { statusId: 3, name: 'Done', colorKey: 'Green', count: 5 },
  ];
}

async function render(breakdown: StatusBreakdownItem[]) {
  TestBed.configureTestingModule({ imports: [StatusDonutChartComponent] });
  const fixture = TestBed.createComponent(StatusDonutChartComponent);
  fixture.componentRef.setInput('breakdown', breakdown);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

describe('StatusDonutChartComponent', () => {
  it("renders a legend entry per status, in the project's own order and names", async () => {
    const fixture = await render(sampleBreakdown());

    const items = fixture.nativeElement.querySelectorAll('.status-donut-legend-item');
    expect(items.length).toBe(3);
    expect(items[0].textContent).toContain('To Do');
    expect(items[1].textContent).toContain('In Progress');
    expect(items[2].textContent).toContain('Done');
  });

  it('shows the correct count per status', async () => {
    const fixture = await render(sampleBreakdown());

    const items = fixture.nativeElement.querySelectorAll('.status-donut-legend-item');
    expect(items[2].textContent).toContain('5');
  });

  it("colors each legend swatch using the status's own colorKey", async () => {
    const fixture = await render(sampleBreakdown());

    const swatches = fixture.nativeElement.querySelectorAll('.legend-swatch');
    expect(swatches[2].style.background).toContain('--color-chip-green-text');
  });

  it('renders one SVG arc per non-zero status', async () => {
    const fixture = await render(sampleBreakdown());

    expect(fixture.nativeElement.querySelectorAll('.donut-segment').length).toBe(3);
  });
});
