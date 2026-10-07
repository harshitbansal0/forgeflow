import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { filter, tap } from 'rxjs';
import { WorkflowsApi } from '../../core/api/admin.api';
import { ChangesApi } from '../../core/api/changes.api';
import { ComponentsApi } from '../../core/api/components.api';
import { ProductsApi } from '../../core/api/products.api';
import { CHANGE_PRIORITIES, ChangePriority } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { ItemPickerData, ItemPickerDialog, PickedItem } from './item-picker-dialog';

@Component({
  selector: 'app-change-create',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule
  ],
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <a class="back-link" routerLink="/changes"><mat-icon>arrow_back</mat-icon> Engineering changes</a>
          <h1>New engineering change</h1>
          <p class="subtitle">The change is saved as a draft. Submit it from the change page to start the approval workflow.</p>
        </div>
      </header>

      <form class="two-column" [formGroup]="form" (ngSubmit)="save()">
        <section class="panel">
          <div class="form-grid">
            <mat-form-field class="full-width">
              <mat-label>Title</mat-label>
              <input matInput formControlName="title" />
              @if (form.controls.title.invalid) {
                <mat-error>A title is required (max 200 characters).</mat-error>
              }
            </mat-form-field>
            <mat-form-field class="full-width">
              <mat-label>Description</mat-label>
              <textarea matInput rows="4" formControlName="description"></textarea>
              @if (form.controls.description.invalid) {
                <mat-error>Describe the change (max 4000 characters).</mat-error>
              }
            </mat-form-field>
            <mat-form-field class="full-width">
              <mat-label>Reason / justification</mat-label>
              <textarea matInput rows="2" formControlName="reason"></textarea>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Priority</mat-label>
              <mat-select formControlName="priority">
                @for (priority of priorities; track priority) {
                  <mat-option [value]="priority">{{ priority }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Approval workflow</mat-label>
              <mat-select formControlName="workflowDefinitionId">
                @for (workflow of workflows(); track workflow.id) {
                  <mat-option [value]="workflow.id">
                    {{ workflow.name }} ({{ workflow.steps.length }} step{{ workflow.steps.length === 1 ? '' : 's' }})
                  </mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>
        </section>

        <section class="panel">
          <div class="panel-title">
            <h2>Affected items</h2>
            <button mat-stroked-button type="button" (click)="addItem()"><mat-icon>add</mat-icon> Add</button>
          </div>
          <ul class="simple-list">
            @for (item of items(); track item.itemType + item.itemId) {
              <li>
                <div>
                  <span class="primary-line">{{ item.number }}</span> {{ item.name }}
                  <div class="secondary-line">{{ item.itemType }}{{ item.note ? ' · ' + item.note : '' }}</div>
                </div>
                <button mat-icon-button type="button" matTooltip="Remove" aria-label="Remove item" (click)="removeItem(item)">
                  <mat-icon>close</mat-icon>
                </button>
              </li>
            } @empty {
              <li class="muted">Add the products or components this change will revise.</li>
            }
          </ul>
        </section>

        <div class="header-actions">
          <button mat-flat-button type="submit" [disabled]="saving()"><mat-icon>save</mat-icon> Create draft change</button>
          <a mat-button routerLink="/changes">Cancel</a>
        </div>
      </form>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangeCreatePage {
  private readonly changesApi = inject(ChangesApi);
  private readonly productsApi = inject(ProductsApi);
  private readonly componentsApi = inject(ComponentsApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly notify = inject(NotifyService);

  protected readonly priorities = CHANGE_PRIORITIES;
  protected readonly items = signal<PickedItem[]>([]);
  protected readonly saving = signal(false);

  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly form = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    reason: ['', Validators.maxLength(2000)],
    priority: this.fb.control<ChangePriority>('Medium'),
    workflowDefinitionId: this.fb.control<number | null>(null, Validators.required)
  });

  protected readonly workflows = toSignal(
    inject(WorkflowsApi)
      .list(true)
      .pipe(
        tap(workflows => {
          const preferred = workflows.find(w => w.isDefault) ?? workflows[0];
          if (preferred && this.form.controls.workflowDefinitionId.value === null) {
            this.form.controls.workflowDefinitionId.setValue(preferred.id);
          }
        })
      ),
    { initialValue: [] }
  );

  constructor() {
    const params = inject(ActivatedRoute).snapshot.queryParamMap;
    const productId = Number(params.get('productId'));
    const componentId = Number(params.get('componentId'));

    if (productId) {
      this.productsApi.get(productId).subscribe(p =>
        this.items.update(items => [...items, { itemType: 'Product', itemId: p.id, number: p.productNumber, name: p.name, note: null }])
      );
    }

    if (componentId) {
      this.componentsApi.get(componentId).subscribe(c =>
        this.items.update(items => [...items, { itemType: 'Component', itemId: c.id, number: c.partNumber, name: c.name, note: null }])
      );
    }
  }

  protected addItem(): void {
    this.dialog
      .open<ItemPickerDialog, ItemPickerData, PickedItem>(ItemPickerDialog, {
        data: { excluded: this.items().map(i => ({ itemType: i.itemType, itemId: i.itemId })) },
        width: '560px'
      })
      .afterClosed()
      .pipe(filter((item): item is PickedItem => !!item))
      .subscribe(item => this.items.update(items => [...items, item]));
  }

  protected removeItem(item: PickedItem): void {
    this.items.update(items => items.filter(i => i !== item));
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.changesApi
      .create({
        title: value.title.trim(),
        description: value.description.trim(),
        reason: value.reason.trim() || null,
        priority: value.priority,
        workflowDefinitionId: value.workflowDefinitionId,
        affectedItems: this.items().map(i => ({ itemType: i.itemType, itemId: i.itemId, note: i.note }))
      })
      .subscribe({
        next: change => {
          this.notify.success(`${change.changeNumber} created as a draft.`);
          void this.router.navigate(['/changes', change.id]);
        },
        error: () => this.saving.set(false)
      });
  }
}
