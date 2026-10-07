import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, numberAttribute, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { ProductsApi } from '../../core/api/products.api';
import { AuthService } from '../../core/auth/auth.service';
import {
  AuditLogEntry,
  BomItem,
  BomItemInput,
  ProductDetail,
  ProductRevisionDetail,
  RevisionComparison
} from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { openComment, openConfirm } from '../../shared/dialogs';
import { HistoryTimeline } from '../../shared/history-timeline';
import { EnumLabelPipe, StatusChip } from '../../shared/status-chip';
import { BomItemDialog, BomItemDialogData } from './bom-item-dialog';
import { ProductFormData, ProductFormDialog } from './product-form-dialog';

@Component({
  selector: 'app-product-detail',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatListModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    MatTabsModule,
    MatTooltipModule,
    EnumLabelPipe,
    StatusChip,
    HistoryTimeline
  ],
  templateUrl: './product-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductDetailPage {
  readonly id = input.required({ transform: numberAttribute });

  private readonly api = inject(ProductsApi);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  protected readonly product = signal<ProductDetail | null>(null);
  protected readonly revision = signal<ProductRevisionDetail | null>(null);
  protected readonly history = signal<AuditLogEntry[]>([]);
  protected readonly comparison = signal<RevisionComparison | null>(null);
  protected readonly compareFrom = signal<number | null>(null);
  protected readonly compareTo = signal<number | null>(null);

  protected readonly selectedRevisionId = computed(() => this.revision()?.revision.id ?? null);
  protected readonly workingRevision = computed(() =>
    this.product()?.revisions.find(r => r.status === 'Draft' || r.status === 'InReview') ?? null
  );
  protected readonly canEditBom = computed(() => this.auth.canEdit() && this.revision()?.revision.status === 'Draft');
  protected readonly bomColumns = computed(() => {
    const columns = ['partNumber', 'componentName', 'componentType', 'quantity', 'referenceDesignator', 'componentRevision', 'notes'];
    return this.canEditBom() ? [...columns, 'actions'] : columns;
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => this.load(id));
    });
  }

  protected selectRevision(revisionId: number): void {
    this.api.revision(this.id(), revisionId).subscribe(revision => this.revision.set(revision));
  }

  protected edit(): void {
    const product = this.product();
    if (!product) {
      return;
    }

    this.dialog
      .open<ProductFormDialog, ProductFormData, ProductDetail>(ProductFormDialog, { data: { product }, width: '640px' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(updated => {
        this.product.set(updated);
        this.loadHistory();
        this.notify.success(`${updated.productNumber} updated.`);
      });
  }

  protected revise(): void {
    const product = this.product();
    if (!product) {
      return;
    }

    openConfirm(this.dialog, {
      title: 'Start a new revision?',
      message: `A new draft revision of ${product.productNumber} will be created, copying the BOM from the released revision.`,
      confirmText: 'Create revision'
    })
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.revise(product.id))
      )
      .subscribe(revision => {
        this.notify.success(`Rev ${revision.revision.revisionCode} created.`);
        this.load(product.id, revision.revision.id);
      });
  }

  protected raiseChange(): void {
    void this.router.navigate(['/changes/new'], { queryParams: { productId: this.id() } });
  }

  protected delete(): void {
    const product = this.product();
    if (!product) {
      return;
    }

    openConfirm(this.dialog, {
      title: `Delete ${product.productNumber}?`,
      message: 'Only products that were never released can be deleted. This cannot be undone.',
      confirmText: 'Delete',
      danger: true
    })
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.delete(product.id))
      )
      .subscribe(() => {
        this.notify.success(`${product.productNumber} deleted.`);
        void this.router.navigate(['/products']);
      });
  }

  protected editSummary(): void {
    const current = this.revision();
    if (!current) {
      return;
    }

    openComment(this.dialog, {
      title: `Rev ${current.revision.revisionCode} change summary`,
      label: 'What changes in this revision?',
      confirmText: 'Save',
      required: false,
      initialValue: current.revision.changeSummary
    })
      .pipe(switchMap(summary => this.api.updateRevision(this.id(), current.revision.id, summary || null)))
      .subscribe(revision => this.afterRevisionChange(revision));
  }

  protected addBomItem(): void {
    const current = this.revision();
    if (!current) {
      return;
    }

    this.openBomDialog({ excludedComponentIds: current.bomItems.map(i => i.componentId) })
      .pipe(switchMap(input => this.api.addBomItem(this.id(), current.revision.id, input)))
      .subscribe(revision => this.afterRevisionChange(revision, 'Component added to the BOM.'));
  }

  protected editBomItem(item: BomItem): void {
    const current = this.revision();
    if (!current) {
      return;
    }

    this.openBomDialog({ item, excludedComponentIds: [] })
      .pipe(switchMap(input => this.api.updateBomItem(this.id(), current.revision.id, item.id, input)))
      .subscribe(revision => this.afterRevisionChange(revision, 'BOM line updated.'));
  }

  protected removeBomItem(item: BomItem): void {
    const current = this.revision();
    if (!current) {
      return;
    }

    openConfirm(this.dialog, {
      title: `Remove ${item.partNumber}?`,
      message: `${item.componentName} will be removed from Rev ${current.revision.revisionCode}.`,
      confirmText: 'Remove',
      danger: true
    })
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.removeBomItem(this.id(), current.revision.id, item.id))
      )
      .subscribe(revision => this.afterRevisionChange(revision, 'BOM line removed.'));
  }

  protected compare(): void {
    const from = this.compareFrom();
    const to = this.compareTo();
    if (from && to) {
      this.api.compare(this.id(), from, to).subscribe(result => this.comparison.set(result));
    }
  }

  private load(id: number, revisionId?: number): void {
    this.api.get(id).subscribe(product => {
      this.product.set(product);
      const selected = revisionId ?? this.revision()?.revision.id;
      const target = product.revisions.find(r => r.id === selected) ?? product.revisions[0];
      if (target) {
        this.selectRevision(target.id);
      }

      this.compareTo.set(product.revisions[0]?.id ?? null);
      this.compareFrom.set(product.revisions[1]?.id ?? null);
      this.comparison.set(null);
      this.loadHistory();
    });
  }

  private loadHistory(): void {
    this.api.history(this.id()).subscribe(entries => this.history.set(entries));
  }

  private afterRevisionChange(revision: ProductRevisionDetail, message?: string): void {
    this.revision.set(revision);
    this.loadHistory();
    if (message) {
      this.notify.success(message);
    }
  }

  private openBomDialog(data: BomItemDialogData) {
    return this.dialog
      .open<BomItemDialog, BomItemDialogData, BomItemInput>(BomItemDialog, { data, width: '560px' })
      .afterClosed()
      .pipe(filter((input): input is BomItemInput => !!input));
  }
}
