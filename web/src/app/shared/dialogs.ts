import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Observable, filter, map } from 'rxjs';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmText?: string;
  danger?: boolean;
}

@Component({
  selector: 'app-confirm-dialog',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p>{{ data.message }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button [class.danger-button]="data.danger" [mat-dialog-close]="true">
        {{ data.confirmText ?? 'Confirm' }}
      </button>
    </mat-dialog-actions>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}

export interface CommentDialogData {
  title: string;
  message?: string;
  label: string;
  confirmText: string;
  required: boolean;
  danger?: boolean;
  initialValue?: string | null;
}

@Component({
  selector: 'app-comment-dialog',
  imports: [MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, ReactiveFormsModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      @if (data.message) {
        <p>{{ data.message }}</p>
      }
      <mat-form-field class="full-width">
        <mat-label>{{ data.label }}</mat-label>
        <textarea matInput rows="4" [formControl]="comment" cdkFocusInitial></textarea>
        @if (comment.hasError('required')) {
          <mat-error>A comment is required.</mat-error>
        }
        @if (comment.hasError('maxlength')) {
          <mat-error>Keep it under 2000 characters.</mat-error>
        }
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button [class.danger-button]="data.danger" (click)="confirm()">{{ data.confirmText }}</button>
    </mat-dialog-actions>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CommentDialog {
  protected readonly data = inject<CommentDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<CommentDialog, string>>(MatDialogRef);
  protected readonly comment = inject(NonNullableFormBuilder).control(this.data.initialValue ?? '', [
    ...(this.data.required ? [Validators.required] : []),
    Validators.maxLength(2000)
  ]);

  protected confirm(): void {
    if (this.comment.invalid) {
      this.comment.markAsTouched();
      return;
    }

    this.dialogRef.close(this.comment.value.trim());
  }
}

export function openConfirm(dialog: MatDialog, data: ConfirmDialogData): Observable<boolean> {
  return dialog
    .open(ConfirmDialog, { data, width: '440px' })
    .afterClosed()
    .pipe(map(result => result === true));
}

/** Emits the entered comment (possibly empty) when confirmed; completes silently on cancel. */
export function openComment(dialog: MatDialog, data: CommentDialogData): Observable<string> {
  return dialog
    .open<CommentDialog, CommentDialogData, string>(CommentDialog, { data, width: '520px' })
    .afterClosed()
    .pipe(filter((value): value is string => value !== undefined));
}
