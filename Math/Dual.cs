namespace UC
{
    public struct Dual
    {
        public float real;   // a
        public float eps;    // b

        public Dual(float real, float eps = 0f)
        {
            this.real = real;
            this.eps = eps;
        }

        public static Dual operator +(Dual x, Dual y) =>
            new Dual(x.real + y.real, x.eps + y.eps);

        public static Dual operator *(Dual x, Dual y) =>
            new Dual(x.real * y.real, x.real * y.eps + x.eps * y.real);
    }
}
