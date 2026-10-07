import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AuditApi, UsersApi } from '../../core/api/admin.api';
import { AUDIT_ACTIONS, AUDIT_ENTITY_TYPES, AuditLogEntry } from '../../core/models';
import { ChangeDiff } from '../../shared/history-timeline';
import { PagedList } from '../../shared/paged-list';
import { EnumLabelPipe, StatusChip } from '../../shared/status-chip';

const ENTITY_ROUTES: Record<string, string> = {
  Product: '/products',
  Component: '/components',
  EngineeringChange: '/changes'
};

@Component({
  selector: 'app-audit-log',
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
    MatDatepickerModule,
    MatIconModule,
    MatButtonModule,
    MatProgressBarModule,
    EnumLabelPipe,
    StatusChip,
    ChangeDiff
  ],
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <h1>Audit log</h1>
          <p class="subtitle">Every data change and workflow decision, with who made it and when.</p>
        </div>
      </header>

      <div class="filter-bar">
        <mat-form-field class="search-field" subscriptSizing="dynamic">
          <mat-icon matPrefix>search</mat-icon>
          <mat-label>Search summary, user or record id</mat-label>
          <input matInput [formControl]="search" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Record type</mat-label>
          <mat-select value="" (selectionChange)="list.setFilter('entityType', $event.value)">
            <mat-option value="">All</mat-option>
            @for (type of entityTypes; track type) {
              <mat-option [value]="type">{{ type | enumLabel }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Action</mat-label>
          <mat-select value="" (selectionChange)="list.setFilter('action', $event.value)">
            <mat-option value="">All</mat-option>
            @for (action of actions; track action) {
              <mat-option [value]="action">{{ action | enumLabel }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>User</mat-label>
          <mat-select value="" (selectionChange)="list.setFilter('userId', $event.value)">
            <mat-option value="">Anyone</mat-option>
            @for (user of users(); track user.id) {
              <mat-option [value]="user.id">{{ user.displayName }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Date range</mat-label>
          <mat-date-range-input [formGroup]="range" [rangePicker]="picker">
            <input matStartDate formControlName="start" placeholder="From" />
            <input matEndDate formControlName="end" placeholder="To" />
          </mat-date-range-input>
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-date-range-picker #picker />
        </mat-form-field>
      </div>

      <div class="table-container">
        @if (list.loading()) {
          <mat-progress-bar mode="indeterminate" />
        }
        <table
          mat-table
          class="data-table"
          multiTemplateDataRows
          [dataSource]="list.items()"
          matSort
          [matSortActive]="list.sort().active"
          [matSortDirection]="list.sort().direction"
          (matSortChange)="list.onSort($event)"
        >
          <ng-container matColumnDef="timestamp">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>When</th>
            <td mat-cell *matCellDef="let entry" class="nowrap">{{ entry.timestampUtc | date: 'medium' }}</td>
          </ng-container>
          <ng-container matColumnDef="userName">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>User</th>
            <td mat-cell *matCellDef="let entry" class="nowrap">{{ entry.userName }}</td>
          </ng-container>
          <ng-container matColumnDef="action">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Action</th>
            <td mat-cell *matCellDef="let entry"><app-status-chip [value]="entry.action" /></td>
          </ng-container>
          <ng-container matColumnDef="entityType">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Record</th>
            <td mat-cell *matCellDef="let entry" class="nowrap">
              @let target = recordLink(entry);
              @if (target) {
                <a [routerLink]="target" (click)="$event.stopPropagation()">{{ entry.entityType | enumLabel }} #{{ entry.entityId }}</a>
              } @else {
                {{ entry.entityType | enumLabel }} #{{ entry.entityId }}
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="summary">
            <th mat-header-cell *matHeaderCellDef>Summary</th>
            <td mat-cell *matCellDef="let entry">{{ entry.summary }}</td>
          </ng-container>
          <ng-container matColumnDef="expand">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let entry" class="actions-cell">
              @if (entry.changes.length) {
                <mat-icon>{{ expanded() === entry.id ? 'expand_less' : 'expand_more' }}</mat-icon>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="detail">
            <td mat-cell *matCellDef="let entry" [attr.colspan]="columns.length">
              @if (expanded() === entry.id) {
                <div style="padding: 8px 0 16px">
                  @if (entry.parentEntityType) {
                    <p class="muted">Part of {{ entry.parentEntityType | enumLabel }} #{{ entry.parentEntityId }}</p>
                  }
                  <app-change-diff [changes]="entry.changes" />
                </div>
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns; sticky: true"></tr>
          <tr mat-row *matRowDef="let row; columns: columns" class="clickable-row" (click)="toggle(row)"></tr>
          <tr mat-row *matRowDef="let row; columns: ['detail']" class="detail-row"></tr>
          <tr class="mat-mdc-row" *matNoDataRow>
            <td class="mat-mdc-cell empty" [attr.colspan]="columns.length">
              {{ list.loading() ? 'Loading…' : 'No audit entries match the current filters.' }}
            </td>
          </tr>
        </table>
      </div>

      <mat-paginator
        [length]="list.result().totalCount"
        [pageIndex]="list.pageIndex()"
        [pageSize]="list.pageSize()"
        [pageSizeOptions]="[20, 50, 100]"
        (page)="list.onPage($event)"
      />
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AuditLogPage {
  private readonly api = inject(AuditApi);

  protected readonly users = toSignal(inject(UsersApi).directory(), { initialValue: [] });
  protected readonly entityTypes = AUDIT_ENTITY_TYPES;
  protected readonly actions = AUDIT_ACTIONS;
  protected readonly columns = ['timestamp', 'userName', 'action', 'entityType', 'summary', 'expand'];
  protected readonly expanded = signal<number | null>(null);
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly range = new FormGroup({
    start: new FormControl<Date | null>(null),
    end: new FormControl<Date | null>(null)
  });
  protected readonly list = new PagedList<AuditLogEntry>(query => this.api.list(query), { active: 'timestamp', direction: 'desc' });

  constructor() {
    this.list.connectSearch(this.search);
    this.range.valueChanges.pipe(takeUntilDestroyed()).subscribe(({ start, end }) => {
      // The API treats the upper bound as exclusive, so include the whole end day.
      const exclusiveEnd = end ? new Date(end.getFullYear(), end.getMonth(), end.getDate() + 1) : null;
      this.list.setFilter('fromUtc', start ? start.toISOString() : null);
      this.list.setFilter('toUtc', exclusiveEnd ? exclusiveEnd.toISOString() : null);
    });
  }

  protected toggle(entry: AuditLogEntry): void {
    this.expanded.update(id => (id === entry.id ? null : entry.id));
  }

  protected recordLink(entry: AuditLogEntry): string[] | null {
    if (ENTITY_ROUTES[entry.entityType] && entry.action !== 'Deleted') {
      return [ENTITY_ROUTES[entry.entityType], entry.entityId];
    }

    const parentRoute = entry.parentEntityType ? ENTITY_ROUTES[entry.parentEntityType] : undefined;
    return parentRoute && entry.parentEntityId ? [parentRoute, entry.parentEntityId] : null;
  }
}
