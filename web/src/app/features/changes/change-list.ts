import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { ChangesApi } from '../../core/api/changes.api';
import { AuthService } from '../../core/auth/auth.service';
import { CHANGE_PRIORITIES, CHANGE_STATUSES, ChangeSummary } from '../../core/models';
import { PagedList } from '../../shared/paged-list';
import { EnumLabelPipe, StatusChip } from '../../shared/status-chip';

@Component({
  selector: 'app-change-list',
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatIconModule,
    MatButtonModule,
    MatProgressBarModule,
    EnumLabelPipe,
    StatusChip
  ],
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <h1>Engineering changes</h1>
          <p class="subtitle">Change orders route draft revisions through approval and release them.</p>
        </div>
        @if (auth.canEdit()) {
          <a mat-flat-button routerLink="/changes/new"><mat-icon>add</mat-icon> New change</a>
        }
      </header>

      <div class="filter-bar">
        <mat-form-field class="search-field" subscriptSizing="dynamic">
          <mat-icon matPrefix>search</mat-icon>
          <mat-label>Search title or ECO number</mat-label>
          <input matInput [formControl]="search" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Status</mat-label>
          <mat-select value="" (selectionChange)="list.setFilter('status', $event.value)">
            <mat-option value="">All</mat-option>
            @for (status of statuses; track status) {
              <mat-option [value]="status">{{ status | enumLabel }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Priority</mat-label>
          <mat-select value="" (selectionChange)="list.setFilter('priority', $event.value)">
            <mat-option value="">All</mat-option>
            @for (priority of priorities; track priority) {
              <mat-option [value]="priority">{{ priority }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-checkbox (change)="list.setFilter('requestedByMe', $event.checked || null)">Requested by me</mat-checkbox>
      </div>

      <div class="table-container">
        @if (list.loading()) {
          <mat-progress-bar mode="indeterminate" />
        }
        <table
          mat-table
          class="data-table"
          [dataSource]="list.items()"
          matSort
          [matSortActive]="list.sort().active"
          [matSortDirection]="list.sort().direction"
          (matSortChange)="list.onSort($event)"
        >
          <ng-container matColumnDef="changeNumber">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Number</th>
            <td mat-cell *matCellDef="let change">
              <a class="number-link" [routerLink]="['/changes', change.id]" (click)="$event.stopPropagation()">{{ change.changeNumber }}</a>
            </td>
          </ng-container>
          <ng-container matColumnDef="title">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Title</th>
            <td mat-cell *matCellDef="let change">
              {{ change.title }}
              <div class="muted">{{ change.affectedItemCount }} affected item{{ change.affectedItemCount === 1 ? '' : 's' }} · {{ change.workflowName }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
            <td mat-cell *matCellDef="let change">
              <app-status-chip [value]="change.status" />
              @if (change.currentStepName) {
                <div class="muted">{{ change.currentStepName }}</div>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="priority">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Priority</th>
            <td mat-cell *matCellDef="let change"><app-status-chip [value]="change.priority" /></td>
          </ng-container>
          <ng-container matColumnDef="requestedBy">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Requested by</th>
            <td mat-cell *matCellDef="let change">{{ change.requestedBy }}</td>
          </ng-container>
          <ng-container matColumnDef="updatedAt">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Last activity</th>
            <td mat-cell *matCellDef="let change">{{ change.updatedAtUtc | date: 'medium' }}</td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns; sticky: true"></tr>
          <tr mat-row *matRowDef="let row; columns: columns" class="clickable-row" (click)="open(row)"></tr>
          <tr class="mat-mdc-row" *matNoDataRow>
            <td class="mat-mdc-cell empty" [attr.colspan]="columns.length">
              {{ list.loading() ? 'Loading…' : 'No engineering changes match the current filters.' }}
            </td>
          </tr>
        </table>
      </div>

      <mat-paginator
        [length]="list.result().totalCount"
        [pageIndex]="list.pageIndex()"
        [pageSize]="list.pageSize()"
        [pageSizeOptions]="[10, 20, 50, 100]"
        (page)="list.onPage($event)"
      />
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangeListPage {
  private readonly api = inject(ChangesApi);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  protected readonly columns = ['changeNumber', 'title', 'status', 'priority', 'requestedBy', 'updatedAt'];
  protected readonly statuses = CHANGE_STATUSES;
  protected readonly priorities = CHANGE_PRIORITIES;
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly list = new PagedList<ChangeSummary>(query => this.api.list(query), { active: 'updatedAt', direction: 'desc' });

  constructor() {
    this.list.connectSearch(this.search);
  }

  protected open(change: ChangeSummary): void {
    void this.router.navigate(['/changes', change.id]);
  }
}
