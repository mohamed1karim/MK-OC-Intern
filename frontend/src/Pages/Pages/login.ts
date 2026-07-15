import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../app/services/login';

@Component({
  selector: 'app-login',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './login.html',
})
export class Login {
  // Signals, not plain properties — this app has no zone.js, so a plain
  // property mutated inside an RxJS subscribe() callback never triggers a
  // view update. Signals do.
  mode = signal<'login' | 'signup'>('login');
  error = signal<string | null>(null);
  successMessage = signal<string | null>(null);
  loading = signal(false);

  form: FormGroup;

  constructor(private fb: FormBuilder, private auth: AuthService, private router: Router) {
    this.form = this.fb.group({
      username: ['', [Validators.required]],
      email: [''],
      password: ['', [Validators.required, Validators.minLength(4)]],
    });
  }

  get username() {
    return this.form.get('username')!;
  }

  get email() {
    return this.form.get('email')!;
  }

  get password() {
    return this.form.get('password')!;
  }

  toggleMode(): void {
    this.mode.set(this.mode() === 'login' ? 'signup' : 'login');
    this.error.set(null);
    this.successMessage.set(null);

    // Email is only required (and validated as an email) in signup mode —
    // toggling re-applies the right validator set rather than keeping one
    // FormGroup shape that's wrong for half of what it's used for.
    if (this.mode() === 'signup') {
      this.email.setValidators([Validators.required, Validators.email]);
    } else {
      this.email.clearValidators();
    }
    this.email.updateValueAndValidity();
  }

  submit(): void {
    this.error.set(null);
    this.successMessage.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    if (this.mode() === 'login') {
      this.doLogin();
    } else {
      this.doSignup();
    }
  }

  private doLogin(): void {
    this.loading.set(true);

    this.auth.login(this.username.value, this.password.value).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/home']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(
          err.status === 401
            ? 'Invalid username or password.'
            : 'Could not log in. Is the API running?'
        );
      },
    });
  }

  private doSignup(): void {
    this.loading.set(true);

    // Auto-login right after signup, using the same credentials just
    // submitted — smoother than making a brand-new user re-type their
    // password immediately after choosing it.
    this.auth.signup(this.username.value, this.email.value, this.password.value).subscribe({
      next: () => this.doLogin(),
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.error ?? 'Could not create account.');
      },
    });
  }
}
