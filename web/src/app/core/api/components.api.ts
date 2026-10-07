import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  AuditLogEntry,
  ComponentDetail,
  ComponentInput,
  ComponentRevisionInput,
  ComponentSummary,
  PagedResult,
  WhereUsed
} from '../models';
import { API_BASE, QueryParams, toHttpParams } from './http';

@Injectable({ providedIn: 'root' })
export class ComponentsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE}/components`;

  list(query: QueryParams) {
    return this.http.get<PagedResult<ComponentSummary>>(this.base, { params: toHttpParams(query) });
  }

  get(id: number) {
    return this.http.get<ComponentDetail>(`${this.base}/${id}`);
  }

  create(input: ComponentInput) {
    return this.http.post<ComponentDetail>(this.base, input);
  }

  update(id: number, input: ComponentInput) {
    return this.http.put<ComponentDetail>(`${this.base}/${id}`, input);
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  revise(id: number) {
    return this.http.post<ComponentDetail>(`${this.base}/${id}/revisions`, {});
  }

  updateRevision(id: number, revisionId: number, input: ComponentRevisionInput) {
    return this.http.put<ComponentDetail>(`${this.base}/${id}/revisions/${revisionId}`, input);
  }

  whereUsed(id: number) {
    return this.http.get<WhereUsed[]>(`${this.base}/${id}/where-used`);
  }

  history(id: number) {
    return this.http.get<AuditLogEntry[]>(`${this.base}/${id}/history`);
  }
}
