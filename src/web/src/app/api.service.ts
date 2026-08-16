import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Product { id: number; code: string; description: string; balance: number; }
export interface InvoiceItem { productId: number; productDescription: string; quantity: number; }
export interface Invoice { id: number; number: number; status: 'Open' | 'Closed'; createdAtUtc: string; closedAtUtc?: string; items: InvoiceItem[]; }

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly inventoryUrl = 'http://localhost:5101';
  private readonly billingUrl = 'http://localhost:5102';
  products(): Observable<Product[]> { return this.http.get<Product[]>(`${this.inventoryUrl}/products`); }
  createProduct(body: Omit<Product, 'id'>): Observable<Product> { return this.http.post<Product>(`${this.inventoryUrl}/products`, body); }
  invoices(): Observable<Invoice[]> { return this.http.get<Invoice[]>(`${this.billingUrl}/invoices`); }
  createInvoice(items: InvoiceItem[]): Observable<Invoice> { return this.http.post<Invoice>(`${this.billingUrl}/invoices`, { items }); }
  printInvoice(id: number): Observable<Invoice> { return this.http.post<Invoice>(`${this.billingUrl}/invoices/${id}/print`, {}); }
  simulateFailure(enabled: boolean): Observable<object> { return this.http.post(`${this.inventoryUrl}/admin/simulate-failure`, { enabled }); }
}
