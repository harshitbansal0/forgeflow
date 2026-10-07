import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  AuditLogEntry,
  CreateUserInput,
  Dashboard,
  DirectoryEntry,
  PagedResult,
  SearchResult,
  UpdateUserInput,
  UserSummary,
  Workflow,
  WorkflowInput
} from '../models';
import { API_BASE, QueryParams, toHttpParams } from './http';

@Injectable({ providedIn: 'root' })
export class WorkflowsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE}/workflows`;

  list(activeOnly = false) {
    return this.http.get<Workflow[]>(this.base, { params: toHttpParams({ activeOnly }) });
  }

  get(id: number) {
    return this.http.get<Workflow>(`${this.base}/${id}`);
  }

  create(input: WorkflowInput) {
    return this.http.post<Workflow>(this.base, input);
  }

  update(id: number, input: WorkflowInput) {
    return this.http.put<Workflow>(`${this.base}/${id}`, input);
  }
}

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE}/users`;

  list(query: QueryParams) {
    return this.http.get<PagedResult<UserSummary>>(this.base, { params: toHttpParams(query) });
  }

  directory() {
    return this.http.get<DirectoryEntry[]>(`${this.base}/directory`);
  }

  create(input: CreateUserInput) {
    return this.http.post<UserSummary>(this.base, input);
  }

  update(id: number, input: UpdateUserInput) {
    return this.http.put<UserSummary>(`${this.base}/${id}`, input);
  }
}

@Injectable({ providedIn: 'root' })
export class AuditApi {
  private readonly http = inject(HttpClient);

  list(query: QueryParams) {
    return this.http.get<PagedResult<AuditLogEntry>>(`${API_BASE}/audit-logs`, { params: toHttpParams(query) });
  }
}

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);

  get() {
    return this.http.get<Dashboard>(`${API_BASE}/dashboard`);
  }
}

@Injectable({ providedIn: 'root' })
export class SearchApi {
  private readonly http = inject(HttpClient);

  search(term: string) {
    return this.http.get<SearchResult[]>(`${API_BASE}/search`, { params: toHttpParams({ q: term }) });
  }
}
