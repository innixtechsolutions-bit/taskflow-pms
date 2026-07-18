import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ProjectDetail, ProjectsService } from '../projects.service';
import { WorkItem, WorkItemsService } from '../work-items.service';
import { AuthService } from '../../auth/auth.service';

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './project-detail.component.html',
})
export class ProjectDetailComponent implements OnInit {
  private readonly projectsService = inject(ProjectsService);
  private readonly workItemsService = inject(WorkItemsService);
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly projectId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly workItems = signal<WorkItem[]>([]);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.project.set(await this.projectsService.getProject(this.projectId));
    const page = await this.workItemsService.getWorkItems(this.projectId);
    this.workItems.set(page.items);
  }

  // Broader than canDelete: also allows the item's current assignee (FR-016).
  protected canEdit(item: WorkItem): boolean {
    const userId = this.authService.currentUser()?.id;
    if (userId === undefined) {
      return false;
    }
    return item.createdByUserId === userId || item.assigneeUserId === userId || this.isManagerOrAdmin();
  }

  // Narrower than canEdit: the current assignee alone cannot delete (FR-017/FR-018).
  protected canDelete(item: WorkItem): boolean {
    const userId = this.authService.currentUser()?.id;
    if (userId === undefined) {
      return false;
    }
    return item.createdByUserId === userId || this.isManagerOrAdmin();
  }

  private isManagerOrAdmin(): boolean {
    const role = this.authService.currentRole();
    return role === 'Manager' || role === 'Admin';
  }

  protected async onDelete(item: WorkItem): Promise<void> {
    if (!confirm(`Delete "${item.title}"? This cannot be undone.`)) {
      return;
    }
    await this.workItemsService.deleteWorkItem(item.id);
    await this.load();
  }

  // Project edit/delete is a simple role check — no ownership dimension, unlike
  // work-item edit/delete (research.md §1).
  protected canManageProject(): boolean {
    return this.isManagerOrAdmin();
  }

  protected async onDeleteProject(): Promise<void> {
    const project = this.project();
    if (!project) {
      return;
    }
    // Uses totalWorkItemCount already fetched to render this page, rather than a
    // second dedicated endpoint (research.md §5).
    const confirmed = confirm(
      `Delete "${project.name}"? This will also delete ${project.totalWorkItemCount} work item(s). This cannot be undone.`
    );
    if (!confirmed) {
      return;
    }
    await this.projectsService.deleteProject(project.id);
    await this.router.navigateByUrl('/projects');
  }
}
