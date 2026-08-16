import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, finalize, forkJoin, takeUntil } from 'rxjs';
import { ApiService, Invoice, InvoiceItem, Product } from './api.service';

@Component({ selector: 'app-root', standalone: true, imports: [CommonModule, FormsModule], templateUrl: './app.html' })
export class AppComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService); private readonly destroyed$ = new Subject<void>();
  products: Product[] = []; invoices: Invoice[] = []; tab: 'products' | 'invoices' = 'products';
  product = { code: '', description: '', balance: 0 }; selectedProductId = 0; quantity = 1; draft: InvoiceItem[] = [];
  loading = false; printingId?: number; failureEnabled = false; message = ''; error = '';

  ngOnInit(): void { this.refresh(); }
  ngOnDestroy(): void { this.destroyed$.next(); this.destroyed$.complete(); }
  refresh(): void { this.loading = true; forkJoin({ products: this.api.products(), invoices: this.api.invoices() }).pipe(takeUntil(this.destroyed$), finalize(() => this.loading = false)).subscribe({ next: x => { this.products = x.products; this.invoices = x.invoices; }, error: e => this.showError(e) }); }
  saveProduct(): void { this.clear(); this.api.createProduct(this.product).pipe(takeUntil(this.destroyed$)).subscribe({ next: () => { this.product = { code: '', description: '', balance: 0 }; this.message = 'Produto cadastrado.'; this.refresh(); }, error: e => this.showError(e) }); }
  addItem(): void { const p = this.products.find(x => x.id === Number(this.selectedProductId)); if (!p || this.quantity <= 0) return; const old = this.draft.find(x => x.productId === p.id); if (old) old.quantity += this.quantity; else this.draft.push({ productId: p.id, productDescription: p.description, quantity: this.quantity }); this.selectedProductId = 0; this.quantity = 1; }
  removeItem(index: number): void { this.draft.splice(index, 1); }
  saveInvoice(): void { this.clear(); this.api.createInvoice(this.draft).pipe(takeUntil(this.destroyed$)).subscribe({ next: () => { this.draft = []; this.message = 'Nota criada com status Aberta.'; this.refresh(); }, error: e => this.showError(e) }); }
  print(invoice: Invoice): void { this.clear(); this.printingId = invoice.id; this.api.printInvoice(invoice.id).pipe(takeUntil(this.destroyed$), finalize(() => this.printingId = undefined)).subscribe({ next: () => { this.message = `Nota ${invoice.number} impressa e fechada; estoque atualizado.`; this.refresh(); }, error: e => this.showError(e) }); }
  toggleFailure(): void { this.api.simulateFailure(this.failureEnabled).pipe(takeUntil(this.destroyed$)).subscribe({ next: () => this.message = this.failureEnabled ? 'Falha do estoque ativada para demonstração.' : 'Serviço de estoque recuperado.', error: e => this.showError(e) }); }
  private clear(): void { this.message = ''; this.error = ''; }
  private showError(e: any): void { this.error = e?.error?.detail ?? e?.error?.message ?? 'Não foi possível concluir a operação.'; }
}
