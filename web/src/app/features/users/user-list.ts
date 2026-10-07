import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { filter } from 'rxjs';
import { UsersApi } from '../../core/api/admin.api';
import { AuthService } from '../../core/auth/auth.service';
import { USER_ROLES, UserRole, UserSummary } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { PagedList } from '../../shared/paged-list';

@Component({
  selector: 'app-user-form-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatSlideToggleModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ user ? 'Edit ' + user.displayName : 'New user' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-grid" id="user-form" (ngSubmit)="save()">
        <mat-form-field class="full-width">
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email" />
          @if (form.controls.email.invalid) {
            <mat-error>Enter a valid email address.</mat-error>
          }
        </mat-form-field>
        <mat-form-field class="full-width">
          <mat-label>Display name</mat-label>
          <input matInput formControlName="displayName" />
          @if (form.controls.displayName.invalid) {
            <mat-error>2-120 characters.</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>Role</mat-label>
          <mat-select formControlName="role">
            @for (role of roles; track role) {
              <mat-option [value]="role">{{ role }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (user) {
          <mat-slide-toggle formControlName="isActive">Active</mat-slide-toggle>
        } @else {
          <mat-form-field>
            <mat-label>Initial password</mat-label>
            <input matInput type="password" formControlName="password" autocomplete="new-password" />
            @if (form.controls.password.invalid) {
              <mat-error>At least 8 characters with a letter and a digit.</mat-error>
            }
          </mat-form-field>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button type="submit" form="user-form" [disabled]="saving()">{{ user ? 'Save user' : 'Create user' }}</button>
    </mat-dialog-actions>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserFormDialog {
  protected readonly user = inject<UserSummary | null>(MAT_DIALOG_DATA);
  private readonly api = inject(UsersApi);
  private readonly dialogRef = inject<MatDialogRef<UserFormDialog, UserSummary>>(MatDialogRef);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly roles = USER_ROLES;
  protected readonly saving = signal(false);
  protected readonly form = this.fb.group({
    email: this.fb.control({ value: this.user?.email ?? '', disabled: !!this.user }, [Validators.required, Validators.email]),
    displayName: [this.user?.displayName ?? '', [Validators.required, Validators.minLength(2), Validators.maxLength(120)]],
    role: this.fb.control<UserRole>(this.user?.role ?? 'Viewer'),
    isActive: [this.user?.isActive ?? true],
    password: this.fb.control(
      { value: '', disabled: !!this.user },
      [Validators.required, Validators.minLength(8), Validators.maxLength(128), Validators.pattern(/^(?=.*[A-Za-z])(?=.*\d).+$/)]
    )
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    const request = this.user
      ? this.api.update(this.user.id, { displayName: value.displayName.trim(), role: value.role, isActive: value.isActive })
      : this.api.create({ email: value.email.trim(), displayName: value.displayName.trim(), role: value.role, password: value.password });
    request.subscribe({
      next: user => this.dialogRef.close(user),
      error: () => this.saving.set(false)
    });
  }
}

@Component({
  selector: 'app-user-list',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatButtonModule,
    MatProgressBarModule,
    MatTooltipModule
  ],
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <h1>Users</h1>
          <p class="subtitle">Accounts and roles. Roles decide what each person can edit, approve and see.</p>
        </div>
        <button mat-flat-button (click)="edit(null)"><mat-icon>person_add</mat-icon> New user</button>
      </header>

      <div class="filter-bar">
        <mat-form-field class="search-field" subscriptSizing="dynamic">
          <mat-icon matPrefix>search</mat-icon>
          <mat-label>Search name or email</mat-label>
          <input matInput [formControl]="search" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Role</mat-label>
          <mat-select value="" (selectionChange)="list.setFilter('role', $event.value)">
            <mat-option value="">All</mat-option>
            @for (role of roles; track role) {
              <mat-option [value]="role">{{ role }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
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
          <ng-container matColumnDef="displayName">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Name</th>
            <td mat-cell *matCellDef="let user">
              {{ user.displayName }}
              @if (user.id === auth.user()?.id) {
                <span class="muted">(you)</span>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="email">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Email</th>
            <td mat-cell *matCellDef="let user">{{ user.email }}</td>
          </ng-container>
          <ng-container matColumnDef="role">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Role</th>
            <td mat-cell *matCellDef="let user">{{ user.role }}</td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let user">
              <span class="status-chip" [attr.data-tone]="user.isActive ? 'success' : 'muted'">{{ user.isActive ? 'Active' : 'Inactive' }}</span>
            </td>
          </ng-container>
          <ng-container matColumnDef="lastLogin">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Last sign-in</th>
            <td mat-cell *matCellDef="let user">{{ user.lastLoginAtUtc ? (user.lastLoginAtUtc | date: 'medium') : 'Never' }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let user" class="actions-cell">
              <button mat-icon-button matTooltip="Edit user" aria-label="Edit user" (click)="edit(user)"><mat-icon>edit</mat-icon></button>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns; sticky: true"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
        </table>
      </div>

      <mat-paginator
        [length]="list.result().totalCount"
        [pageIndex]="list.pageIndex()"
        [pageSize]="list.pageSize()"
        [pageSizeOptions]="[10, 20, 50]"
        (page)="list.onPage($event)"
      />
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserListPage {
  private readonly api = inject(UsersApi);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);
  protected readonly auth = inject(AuthService);

  protected readonly roles = USER_ROLES;
  protected readonly columns = ['displayName', 'email', 'role', 'status', 'lastLogin', 'actions'];
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly list = new PagedList<UserSummary>(query => this.api.list(query), { active: 'displayName', direction: 'asc' });

  constructor() {
    this.list.connectSearch(this.search);
  }

  protected edit(user: UserSummary | null): void {
    this.dialog
      .open<UserFormDialog, UserSummary | null, UserSummary>(UserFormDialog, { data: user, width: '560px' })
      .afterClosed()
      .pipe(filter((saved): saved is UserSummary => !!saved))
      .subscribe(saved => {
        this.notify.success(`${saved.displayName} saved.`);
        this.list.refresh();
      });
  }
}
