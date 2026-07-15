import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ProductService } from '../../../app/services/product';
import { AuthService } from '../../../app/services/login';

@Component({
  selector: 'app-add-product',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './add-product.html',
})
export class AddProduct {
  loading = signal(false);
  error = signal<string | null>(null);

  form: FormGroup;

  constructor(
    private fb: FormBuilder,
    private productService: ProductService,
    private router: Router,
    public auth: AuthService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required]],
      description: [''],
      // price/quantity start as null (not 0) so the number inputs render
      // empty instead of pre-filled with a zero the user has to clear first.
      price: [null, [Validators.required, Validators.min(0)]],
      quantity: [null, [Validators.required, Validators.min(0)]],
    });
  }

  get name() {
    return this.form.get('name')!;
  }

  get price() {
    return this.form.get('price')!;
  }

  get quantity() {
    return this.form.get('quantity')!;
  }

  get isAdmin(): boolean {
    const role = this.auth.currentUser()?.role;
    return role === 'Admin' || role === 'SuperAdmin';
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.error.set(null);
    this.loading.set(true);

    this.productService
      .create({
        name: this.form.value.name,
        description: this.form.value.description ?? '',
        price: this.form.value.price,
        quantity: this.form.value.quantity,
      })
      .subscribe({
        next: (created) => {
          this.router.navigate(['/products', created.id]);
        },
        error: (err) => {
          this.error.set(err.error?.error ?? 'Could not add product.');
          this.loading.set(false);
        },
      });
  }
}
