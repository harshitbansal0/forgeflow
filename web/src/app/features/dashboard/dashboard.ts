import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { DashboardApi } from '../../core/api/admin.api';
import { AuthService } from '../../core/auth/auth.service';
import { CountByKey } from '../../core/models';
import { EnumLabelPipe, StatusChip } from '../../shared/status-chip';

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, EnumLabelPipe, StatusChip],
  templateUrl: './dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardPage {
  protected readonly auth = inject(AuthService);
  protected readonly data = toSignal(inject(DashboardApi).get());
  protected readonly firstName = computed(() => this.auth.user()?.displayName.split(' ')[0] ?? '');

  protected percent(row: CountByKey, rows: CountByKey[]): number {
    const total = rows.reduce((sum, r) => sum + r.count, 0);
    return total === 0 ? 0 : Math.round((row.count / total) * 100);
  }

  protected link(entityType: string, entityId: string): string[] | null {
    switch (entityType) {
      case 'Product':
        return ['/products', entityId];
      case 'Component':
        return ['/components', entityId];
      case 'EngineeringChange':
        return ['/changes', entityId];
      default:
        return null;
    }
  }
}
