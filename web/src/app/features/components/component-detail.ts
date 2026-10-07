import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, numberAttribute, signal, untracked } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { ComponentsApi } from '../../core/api/components.api';
import { AuthService } from '../../core/auth/auth.service';
import { AuditLogEntry, ComponentDetail, ComponentRevisionInput, Revision, WhereUsed } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { openConfirm } from '../../shared/dialogs';
import { HistoryTimeline } from '../../shared/history-timeline';
import { EnumLabelPipe, StatusChip } from '../../shared/status-chip';
import { ComponentFormData, ComponentFormDialog } from './component-form-dialog';

@Component({
  selector: 'app-component-revision-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Edit Rev {{ revision.revisionCode }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-grid" id="component-revision-form" (ngSubmit)="save()">
        <mat-form-field>
          <mat-label>Drawing number</mat-label>
          <input matInput formControlName="drawingNumber" />
        </mat-form-field>
        <mat-form-field>
          <mat-label>Weight (kg)</mat-label>
          <input matInput type="number" min="0" step="0.001" formControlName="weightKg" />
        </mat-form-field>
        <mat-form-field class="full-width">
          <mat-label>Change summary</mat-label>
          <textarea matInput rows="3" formControlName="changeSummary"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button type="submit" form="component-revision-form">Save revision</button>
    </mat-dialog-actions>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ComponentRevisionDialog {
  protected readonly revision = inject<Revision>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<ComponentRevisionDialog, ComponentRevisionInput>>(MatDialogRef);
  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly form = this.fb.group({
    drawingNumber: [this.revision.drawingNumber ?? '', Validators.maxLength(60)],
    weightKg: this.fb.control<number | null>(this.revision.weightKg, Validators.min(0)),
    changeSummary: [this.revision.changeSummary ?? '', Validators.maxLength(1000)]
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.dialogRef.close({
      drawingNumber: value.drawingNumber.trim() || null,
      weightKg: value.weightKg,
      changeSummary: value.changeSummary.trim() || null
    });
  }
}

@Component({
  selector: 'app-component-detail',
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    MatTabsModule,
    MatTooltipModule,
    EnumLabelPipe,
    StatusChip,
    HistoryTimeline
  ],
  templateUrl: './component-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ComponentDetailPage {
  readonly id = input.required({ transform: numberAttribute });

  private readonly api = inject(ComponentsApi);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  protected readonly component = signal<ComponentDetail | null>(null);
  protected readonly whereUsed = signal<WhereUsed[]>([]);
  protected readonly history = signal<AuditLogEntry[]>([]);
  protected readonly revisionColumns = ['revisionCode', 'status', 'drawingNumber', 'weightKg', 'changeSummary', 'released', 'actions'];
  protected readonly whereUsedColumns = ['productNumber', 'productName', 'revision', 'quantity', 'referenceDesignator'];
  protected readonly workingRevision = computed(
    () => this.component()?.revisions.find(r => r.status === 'Draft' || r.status === 'InReview') ?? null
  );

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => this.load(id));
    });
  }

  protected edit(): void {
    const component = this.component();
    if (!component) {
      return;
    }

    this.dialog
      .open<ComponentFormDialog, ComponentFormData, ComponentDetail>(ComponentFormDialog, { data: { component }, width: '680px' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(updated => this.afterChange(updated, `${updated.partNumber} updated.`));
  }

  protected revise(): void {
    const component = this.component();
    if (!component) {
      return;
    }

    openConfirm(this.dialog, {
      title: 'Start a new revision?',
      message: `A new draft revision of ${component.partNumber} will be created from the released specification.`,
      confirmText: 'Create revision'
    })
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.revise(component.id))
      )
      .subscribe(updated => this.afterChange(updated, 'New draft revision created.'));
  }

  protected editRevision(revision: Revision): void {
    this.dialog
      .open<ComponentRevisionDialog, Revision, ComponentRevisionInput>(ComponentRevisionDialog, { data: revision, width: '560px' })
      .afterClosed()
      .pipe(
        filter((input): input is ComponentRevisionInput => !!input),
        switchMap(input => this.api.updateRevision(this.id(), revision.id, input))
      )
      .subscribe(updated => this.afterChange(updated, `Rev ${revision.revisionCode} updated.`));
  }

  protected raiseChange(): void {
    void this.router.navigate(['/changes/new'], { queryParams: { componentId: this.id() } });
  }

  protected delete(): void {
    const component = this.component();
    if (!component) {
      return;
    }

    openConfirm(this.dialog, {
      title: `Delete ${component.partNumber}?`,
      message: 'Only components that were never released or used in a BOM can be deleted.',
      confirmText: 'Delete',
      danger: true
    })
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.delete(component.id))
      )
      .subscribe(() => {
        this.notify.success(`${component.partNumber} deleted.`);
        void this.router.navigate(['/components']);
      });
  }

  private load(id: number): void {
    this.api.get(id).subscribe(component => this.component.set(component));
    this.api.whereUsed(id).subscribe(rows => this.whereUsed.set(rows));
    this.loadHistory();
  }

  private loadHistory(): void {
    this.api.history(this.id()).subscribe(entries => this.history.set(entries));
  }

  private afterChange(component: ComponentDetail, message: string): void {
    this.component.set(component);
    this.loadHistory();
    this.notify.success(message);
  }
}
