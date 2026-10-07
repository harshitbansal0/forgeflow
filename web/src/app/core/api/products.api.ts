import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  AuditLogEntry,
  BomItemInput,
  PagedResult,
  ProductDetail,
  ProductInput,
  ProductRevisionDetail,
  ProductSummary,
  RevisionComparison
} from '../models';
import { API_BASE, QueryParams, toHttpParams } from './http';

@Injectable({ providedIn: 'root' })
export class ProductsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE}/products`;

  list(query: QueryParams) {
    return this.http.get<PagedResult<ProductSummary>>(this.base, { params: toHttpParams(query) });
  }

  categories() {
    return this.http.get<string[]>(`${this.base}/categories`);
  }

  get(id: number) {
    return this.http.get<ProductDetail>(`${this.base}/${id}`);
  }

  create(input: ProductInput) {
    return this.http.post<ProductDetail>(this.base, input);
  }

  update(id: number, input: ProductInput) {
    return this.http.put<ProductDetail>(`${this.base}/${id}`, input);
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  revision(productId: number, revisionId: number) {
    return this.http.get<ProductRevisionDetail>(`${this.base}/${productId}/revisions/${revisionId}`);
  }

  revise(productId: number) {
    return this.http.post<ProductRevisionDetail>(`${this.base}/${productId}/revisions`, {});
  }

  updateRevision(productId: number, revisionId: number, changeSummary: string | null) {
    return this.http.put<ProductRevisionDetail>(`${this.base}/${productId}/revisions/${revisionId}`, { changeSummary });
  }

  addBomItem(productId: number, revisionId: number, input: BomItemInput) {
    return this.http.post<ProductRevisionDetail>(`${this.base}/${productId}/revisions/${revisionId}/bom`, input);
  }

  updateBomItem(productId: number, revisionId: number, bomItemId: number, input: BomItemInput) {
    return this.http.put<ProductRevisionDetail>(`${this.base}/${productId}/revisions/${revisionId}/bom/${bomItemId}`, input);
  }

  removeBomItem(productId: number, revisionId: number, bomItemId: number) {
    return this.http.delete<ProductRevisionDetail>(`${this.base}/${productId}/revisions/${revisionId}/bom/${bomItemId}`);
  }

  compare(productId: number, fromRevisionId: number, toRevisionId: number) {
    return this.http.get<RevisionComparison>(`${this.base}/${productId}/revisions/compare`, {
      params: toHttpParams({ from: fromRevisionId, to: toRevisionId })
    });
  }

  history(productId: number) {
    return this.http.get<AuditLogEntry[]>(`${this.base}/${productId}/history`);
  }
}
