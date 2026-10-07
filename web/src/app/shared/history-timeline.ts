import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { AuditLogEntry, AuditPropertyChange } from '../core/models';
import { EnumLabelPipe, StatusChip } from './status-chip';

@Component({
  selector: 'app-change-diff',
  imports: [EnumLabelPipe],
  template: `
    <table class="diff-table">
      <thead>
        <tr>
          <th>Field</th>
          <th>Before</th>
          <th>After</th>
        </tr>
      </thead>
      <tbody>
        @for (change of changes(); track change.property) {
          <tr>
            <td>{{ change.property | enumLabel }}</td>
            <td class="old-value">{{ change.oldValue ?? '—' }}</td>
            <td class="new-value">{{ change.newValue ?? '—' }}</td>
          </tr>
        }
      </tbody>
    </table>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangeDiff {
  readonly changes = input.required<AuditPropertyChange[]>();
}

@Component({
  selector: 'app-history-timeline',
  imports: [DatePipe, EnumLabelPipe, StatusChip, ChangeDiff],
  template: `
    @if (entries().length === 0) {
      <p class="empty">No history recorded yet.</p>
    } @else {
      <ol class="timeline">
        @for (entry of entries(); track entry.id) {
          <li class="timeline-item">
            <div>
              <app-status-chip [value]="entry.action" />
              <strong> {{ entry.summary || (entry.entityType | enumLabel) }}</strong>
            </div>
            <div class="timeline-meta">
              {{ entry.userName }} · {{ entry.timestampUtc | date: 'medium' }} · {{ entry.entityType | enumLabel }} #{{ entry.entityId }}
            </div>
            @if (entry.changes.length) {
              <details>
                <summary>{{ entry.changes.length }} field change{{ entry.changes.length === 1 ? '' : 's' }}</summary>
                <app-change-diff [changes]="entry.changes" />
              </details>
            }
          </li>
        }
      </ol>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HistoryTimeline {
  readonly entries = input.required<AuditLogEntry[]>();
}
