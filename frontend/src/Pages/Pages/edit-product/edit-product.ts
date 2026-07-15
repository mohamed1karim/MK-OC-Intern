import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LoadingSpinner } from '../../../app/shared/loading-spinner/loading-spinner';
import { ProductService } from '../../../app/services/product';
import { AuthService } from '../../../app/services/login';

// Admin/SuperAdmin only — mirrors AddProduct, but pre-filled from the
// existing product and PUTs instead of POSTs. No quantity field: stock can
// only change through Orders, never by editing a product directly.
@Component({
  selector: 'app-edit-product',
  imports: [CommonModule, ReactiveFormsModule, RouterLink, LoadingSpinner],
  templateUrl: './edit-product.html',
})
export class EditProduct implements OnInit {
  productId = 0;
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  notFound = signal(false);

  form: FormGroup;

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private productService: ProductService,
    public auth: AuthService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required]],
      description: [''],
      price: [null, [Validators.required, Validators.min(0)]],
    });
  }

  get name() {
    return this.form.get('name')!;
  }

  get price() {
    return this.form.get('price')!;
  }

  get isAdmin(): boolean {
    const role = this.auth.currentUser()?.role;
    return role === 'Admin' || role === 'SuperAdmin';
  }

  ngOnInit(): void {
    this.productId = Number(this.route.snapshot.paramMap.get('id'));

    this.productService.getById(this.productId).subscribe({
      next: (product) => {
        this.form.patchValue({ name: product.name, description: product.description, price: product.price });
        this.loading.set(false);
      },
      error: (err) => {
        console.error(err);
        this.notFound.set(err.status === 404);
        this.error.set(err.status === 404 ? 'Product not found.' : 'Could not load this product. Is the API running?');
        this.loading.set(false);
      },
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.error.set(null);
    this.saving.set(true);

    this.productService
      .update(this.productId, {
        name: this.form.value.name,
        description: this.form.value.description ?? '',
        price: this.form.value.price,
      })
      .subscribe({
        next: () => {
          this.router.navigate(['/products', this.productId]);
        },
        error: (err) => {
          this.error.set(err.error?.error ?? 'Could not save changes.');
          this.saving.set(false);
        },
      });
  }
}
