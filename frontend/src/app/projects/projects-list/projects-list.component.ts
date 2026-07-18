import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';
import { ProjectListItem, ProjectsService } from '../projects.service';

@Component({
  selector: 'app-projects-list',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatTableModule, PageHeaderComponent],
  templateUrl: './projects-list.component.html',
})
export class ProjectsListComponent implements OnInit {
  private readonly projectsService = inject(ProjectsService);
  protected readonly authService = inject(AuthService);

  // Column order for mat-table's structural directives.
  protected readonly displayedColumns = ['name', 'createdByName', 'createdAt', 'openWorkItemCount', 'actions'];

  protected readonly items = signal<ProjectListItem[]>([]);
  protected readonly page = signal(1);
  protected readonly pageSize = 20;
  protected readonly totalCount = signal(0);

  ngOnInit(): void {
    void this.load();
  }

  protected nextPage(): void {
    this.page.update((p) => p + 1);
    void this.load();
  }

  protected prevPage(): void {
    this.page.update((p) => Math.max(1, p - 1));
    void this.load();
  }

  private async load(): Promise<void> {
    const result = await this.projectsService.getProjects(this.page(), this.pageSize);
    this.items.set(result.items);
    this.totalCount.set(result.totalCount);
  }
}
