import {
  ArrowRight,
  CheckCircle,
  Lightning,
  Pulse,
  UsersThree,
} from "@phosphor-icons/react"

import collaborationIllustration from "@/assets/img/collaboration.svg"
import { Button } from "@/components/ui/button"

const proofPoints = [
  "Shared boards",
  "Live presence",
  "Traceable decisions",
] as const

export function Hero() {
  return (
    <main>
      <section className="relative isolate overflow-hidden px-4 pt-16 pb-20 sm:px-6 sm:pt-20 lg:px-8 lg:pt-24 lg:pb-28">
        <div className="absolute inset-0 -z-10 bg-[radial-gradient(circle_at_14%_18%,rgba(45,212,191,0.18),transparent_28%),radial-gradient(circle_at_78%_12%,rgba(236,72,153,0.12),transparent_24%),linear-gradient(180deg,rgba(250,250,250,0.98)_0%,rgba(244,244,245,0.7)_100%)] dark:bg-[radial-gradient(circle_at_14%_18%,rgba(20,184,166,0.12),transparent_28%),radial-gradient(circle_at_78%_12%,rgba(236,72,153,0.1),transparent_24%),linear-gradient(180deg,rgba(9,9,11,1)_0%,rgba(24,24,27,0.92)_100%)]" />
        <div className="absolute inset-x-0 bottom-0 -z-10 h-px bg-zinc-950/10 dark:bg-white/10" />

        <div className="mx-auto grid max-w-7xl items-center gap-12 lg:grid-cols-[0.92fr_1.08fr] lg:gap-16">
          <div className="max-w-2xl">
            <div className="inline-flex items-center gap-2 rounded-full border border-zinc-950/10 bg-white/78 px-3 py-1.5 text-[12px] font-semibold text-zinc-700 shadow-[0_12px_34px_rgba(24,24,27,0.08)] backdrop-blur-xl dark:border-white/10 dark:bg-white/[0.06] dark:text-zinc-200">
              <span className="grid size-5 place-items-center rounded-full bg-zinc-950 text-white dark:bg-white dark:text-zinc-950">
                <Pulse className="size-3.5" weight="bold" />
              </span>
              Projects, people, pace. In one place.
            </div>

            <h1 className="mt-7 max-w-[12ch] text-6xl leading-[0.92] font-semibold tracking-normal text-zinc-950 sm:text-7xl lg:text-8xl dark:text-white">
              Coordina
            </h1>
            <p className="mt-7 max-w-xl text-xl leading-8 font-medium text-zinc-800 sm:text-2xl sm:leading-9 dark:text-zinc-100">
              Teamwork stays clear, fast and alive, even when projects keep
              moving in every direction.
            </p>
            <p className="mt-5 max-w-xl text-base leading-8 text-zinc-600 dark:text-zinc-300">
              A simple, beautiful project management app for organizing tasks,
              keeping context close, and moving together in real time without
              turning the day into a notification tunnel.
            </p>

            <div className="mt-9 flex flex-col gap-3 sm:flex-row">
              <Button
                asChild
                className="h-12 rounded-full bg-zinc-950 px-6 text-sm text-white shadow-[0_18px_44px_rgba(9,9,11,0.22)] transition-transform duration-300 hover:-translate-y-0.5 hover:bg-zinc-900 dark:bg-white dark:text-zinc-950 dark:hover:bg-zinc-100"
              >
                <a href="/login">
                  Get started
                  <ArrowRight className="size-4" />
                </a>
              </Button>
              <Button
                asChild
                variant="outline"
                className="h-12 rounded-full border-zinc-950/10 bg-white/70 px-6 text-sm text-zinc-800 shadow-[0_12px_34px_rgba(24,24,27,0.07)] backdrop-blur-xl hover:bg-white dark:border-white/10 dark:bg-white/[0.06] dark:text-zinc-100 dark:hover:bg-white/[0.1]"
              >
                <a href="#platform">Explore platform</a>
              </Button>
            </div>

            <div className="mt-9 flex flex-wrap gap-x-5 gap-y-3">
              {proofPoints.map((point) => (
                <span
                  key={point}
                  className="inline-flex items-center gap-2 text-sm font-medium text-zinc-600 dark:text-zinc-300"
                >
                  <CheckCircle
                    className="size-4 text-teal-600 dark:text-teal-300"
                    weight="fill"
                  />
                  {point}
                </span>
              ))}
            </div>
          </div>

          <div className="relative min-h-[420px] lg:min-h-[600px]">
            <div className="absolute top-5 right-0 left-0 mx-auto h-[72%] max-w-[640px] rounded-[44px] border border-zinc-950/[0.08] bg-white/68 shadow-[0_38px_120px_rgba(24,24,27,0.18)] backdrop-blur-2xl dark:border-white/10 dark:bg-white/[0.05] dark:shadow-[0_38px_120px_rgba(0,0,0,0.42)]" />
            <div className="absolute top-0 right-6 left-6 h-28 rounded-full bg-teal-300/22 blur-3xl dark:bg-teal-300/10" />

            <img
              src={collaborationIllustration}
              alt="Illustration of a team collaborating around a project"
              className="relative z-10 mx-auto w-full max-w-[720px] translate-y-4 drop-shadow-[0_30px_50px_rgba(24,24,27,0.16)]"
            />

            <div className="absolute right-0 bottom-8 z-20 w-[min(280px,72vw)] rounded-[26px] border border-zinc-950/[0.08] bg-white/88 p-4 shadow-[0_26px_70px_rgba(24,24,27,0.16)] backdrop-blur-2xl dark:border-white/10 dark:bg-zinc-950/86">
              <div className="flex items-center justify-between gap-4">
                <div>
                  <p className="text-[11px] font-semibold tracking-[0.16em] text-zinc-400 uppercase">
                    Active sprint
                  </p>
                  <p className="mt-1 text-sm font-semibold text-zinc-950 dark:text-white">
                    12 synced tasks
                  </p>
                </div>
                <span className="grid size-10 place-items-center rounded-2xl bg-zinc-950 text-white dark:bg-white dark:text-zinc-950">
                  <Lightning className="size-5" weight="fill" />
                </span>
              </div>
              <div className="mt-4 grid gap-2">
                <div className="h-2 rounded-full bg-zinc-950/10 dark:bg-white/10">
                  <div className="h-full w-[76%] rounded-full bg-teal-500" />
                </div>
                <div className="flex items-center justify-between text-xs font-medium text-zinc-500 dark:text-zinc-400">
                  <span>Delivery</span>
                  <span>76%</span>
                </div>
              </div>
            </div>

            <div className="absolute bottom-0 left-0 z-20 hidden w-[240px] rounded-[26px] border border-zinc-950/[0.08] bg-zinc-950 p-4 text-white shadow-[0_26px_70px_rgba(9,9,11,0.2)] sm:block dark:border-white/10 dark:bg-white dark:text-zinc-950">
              <div className="flex items-center gap-3">
                <span className="grid size-10 place-items-center rounded-2xl bg-white/12 dark:bg-zinc-950/[0.08]">
                  <UsersThree className="size-5" weight="bold" />
                </span>
                <div>
                  <p className="text-sm font-semibold">Team online</p>
                  <p className="mt-1 text-xs text-white/60 dark:text-zinc-500">
                    4 updates just now
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>
    </main>
  )
}
