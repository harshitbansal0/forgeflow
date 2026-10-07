import { ChangeDetectionStrategy, Component, Pipe, PipeTransform, computed, input } from '@angular/core';

/** "InReview" -> "In Review", "EngineeringChange" -> "Engineering Change". */
@Pipe({ name: 'enumLabel' })
export class EnumLabelPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return value ? value.replace(/([a-z])([A-Z])/g, '$1 $2') : '';
  }
}

const TONES: Record<string, string> = {
  Draft: 'neutral',
  InReview: 'warning',
  Released: 'success',
  Superseded: 'muted',
  Approved: 'success',
  Rejected: 'danger',
  Implemented: 'info',
  Cancelled: 'muted',
  Pending: 'muted',
  Active: 'warning',
  Skipped: 'muted',
  Concept: 'violet',
  Development: 'info',
  Production: 'success',
  Obsolete: 'muted',
  Low: 'muted',
  Medium: 'neutral',
  High: 'warning',
  Critical: 'danger',
  Created: 'success',
  Updated: 'info',
  Deleted: 'danger',
  Submitted: 'warning',
  LoginFailed: 'danger',
  Added: 'success',
  Removed: 'danger',
  QuantityChanged: 'warning'
};

@Component({
  selector: 'app-status-chip',
  imports: [EnumLabelPipe],
  template: `<span class="status-chip" [attr.data-tone]="tone()">{{ value() | enumLabel }}</span>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StatusChip {
  readonly value = input.required<string>();
  protected readonly tone = computed(() => TONES[this.value()] ?? 'neutral');
}
