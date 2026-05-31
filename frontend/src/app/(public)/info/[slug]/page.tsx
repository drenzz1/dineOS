import Link from "next/link";
import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { INFO_PAGES, infoSlugs } from "./content";

export const dynamicParams = false;

export function generateStaticParams() {
  return infoSlugs.map((slug) => ({ slug }));
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const page = INFO_PAGES[slug];
  if (!page) {
    return { title: "Not found · dineOS" };
  }
  return { title: `${page.title} · dineOS`, description: page.lede };
}

export default async function InfoPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const page = INFO_PAGES[slug];
  if (!page) {
    notFound();
  }

  return (
    <main id="main-content" className="min-h-screen bg-bg text-fg">
      <nav className="sticky top-0 z-50 border-b border-border bg-bg/80 backdrop-blur-xl">
        <div className="mx-auto flex h-[60px] max-w-3xl items-center px-5 md:px-8">
          <Link href="/" className="flex items-center gap-2.5 text-fg">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-gradient-to-br from-ember-500 to-ember-700 text-[13px] font-bold text-white shadow-sm">
              d
            </span>
            <span className="text-sm font-semibold tracking-[-0.01em]">dineOS</span>
          </Link>
          <Link
            href="/"
            className="ml-auto inline-flex h-[34px] items-center rounded-md px-3.5 text-[13px] font-semibold text-fg-muted hover:text-fg"
          >
            ← Back to home
          </Link>
        </div>
      </nav>

      <article className="mx-auto max-w-3xl px-5 py-16 md:px-8 md:py-20">
        <span className="mb-2.5 block text-[11px] font-semibold uppercase tracking-[0.08em] text-accent">
          {page.eyebrow}
        </span>
        <h1 className="text-[34px] font-semibold leading-tight tracking-[-0.03em] md:text-[44px]">
          {page.title}
        </h1>
        <p className="mt-4 text-[16px] leading-7 text-fg-muted">{page.lede}</p>
        {page.note ? (
          <p className="mt-4 rounded-[10px] border border-border bg-surface px-4 py-3 text-[12.5px] leading-6 text-fg-subtle">
            {page.note}
          </p>
        ) : null}

        <div className="mt-10 space-y-10">
          {page.sections.map((section) => (
            <section key={section.heading ?? section.body?.[0] ?? section.bullets?.[0]}>
              {section.heading ? (
                <h2 className="text-lg font-semibold tracking-[-0.015em]">{section.heading}</h2>
              ) : null}
              {section.body?.map((paragraph) => (
                <p key={paragraph} className="mt-3 text-[14.5px] leading-7 text-fg-muted">
                  {paragraph}
                </p>
              ))}
              {section.bullets ? (
                <ul className="mt-3 space-y-2">
                  {section.bullets.map((bullet) => (
                    <li key={bullet} className="flex gap-2.5 text-[14px] leading-6 text-fg-muted">
                      <span className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
                      {bullet}
                    </li>
                  ))}
                </ul>
              ) : null}
            </section>
          ))}
        </div>

        <div className="mt-14 border-t border-border pt-6">
          <Link href="/" className="text-[13px] font-semibold text-accent">
            ← Back to dineOS
          </Link>
        </div>
      </article>
    </main>
  );
}
