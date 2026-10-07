import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  AffectedItemInput,
  AuditLogEntry,
  ChangeDetail,
  ChangeInput,
  ChangeSummary,
  CreateChangeInput,
  PagedResult,
  PendingApproval
} from '../models';
import { API_BASE, QueryParams, toHttpParams } from './http';

@Injectable({ providedIn: 'root' })
export class ChangesApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE}/changes`;

  list(query: QueryParams) {
    return this.http.get<PagedResult<ChangeSummary>>(this.base, { params: toHttpParams(query) });
  }

  get(id: number) {
    return this.http.get<ChangeDetail>(`${this.base}/${id}`);
  }

  create(input: CreateChangeInput) {
    return this.http.post<ChangeDetail>(this.base, input);
  }

  update(id: number, input: ChangeInput) {
    return this.http.put<ChangeDetail>(`${this.base}/${id}`, input);
  }

  addAffectedItem(id: number, input: AffectedItemInput) {
    return this.http.post<ChangeDetail>(`${this.base}/${id}/affected-items`, input);
  }

  removeAffectedItem(id: number, affectedItemId: number) {
    return this.http.delete<ChangeDetail>(`${this.base}/${id}/affected-items/${affectedItemId}`);
  }

  submit(id: number) {
    return this.http.post<ChangeDetail>(`${this.base}/${id}/submit`, {});
  }

  approve(id: number, comment: string | null) {
    return this.http.post<ChangeDetail>(`${this.base}/${id}/approve`, { comment });
  }

  reject(id: number, comment: string) {
    return this.http.post<ChangeDetail>(`${this.base}/${id}/reject`, { comment });
  }

  implement(id: number) {
    return this.http.post<ChangeDetail>(`${this.base}/${id}/implement`, {});
  }

  cancel(id: number, comment: string | null) {
    return this.http.post<ChangeDetail>(`${this.base}/${id}/cancel`, { comment });
  }

  history(id: number) {
    return this.http.get<AuditLogEntry[]>(`${this.base}/${id}/history`);
  }
}

@Injectable({ providedIn: 'root' })
export class ApprovalsApi {
  private readonly http = inject(HttpClient);

  pending() {
    return this.http.get<PendingApproval[]>(`${API_BASE}/approvals/pending`);
  }
}
