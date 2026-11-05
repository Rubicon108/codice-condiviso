-- Esegui una volta in Supabase SQL Editor.
create extension if not exists pgcrypto with schema extensions;

create table if not exists public.workspaces (
  id uuid primary key default gen_random_uuid(),
  pin_hash text not null unique,
  title text not null default 'Nuovo sketch',
  source text not null default '',
  version bigint not null default 1,
  updated_at timestamptz not null default now()
);
alter table public.workspaces enable row level security;
-- Nessun accesso diretto alle righe. I client usano solo le RPC sottostanti.
revoke all on public.workspaces from anon, authenticated;

create or replace function public.create_workspace(p_pin text, p_title text, p_source text)
returns jsonb language plpgsql security definer set search_path=public,extensions as $$
declare h text; r public.workspaces;
begin
  if p_pin !~ '^\d{6,12}$' then raise exception 'Il codice deve contenere da 6 a 12 cifre'; end if;
  h := encode(digest(p_pin, 'sha256'), 'hex');
  insert into public.workspaces(pin_hash,title,source)
  values(h,coalesce(nullif(left(p_title,80),''),'Nuovo sketch'),coalesce(p_source,''))
  returning * into r;
  return jsonb_build_object('title',r.title,'source',r.source,'version',r.version);
exception when unique_violation then
  raise exception 'Codice già in uso: scegline un altro';
end; $$;

create or replace function public.read_workspace(p_pin text)
returns jsonb language plpgsql security definer set search_path=public,extensions as $$
declare r public.workspaces;
begin
  select * into r from public.workspaces where pin_hash=encode(digest(p_pin,'sha256'),'hex');
  if not found then return null; end if;
  return jsonb_build_object('title',r.title,'source',r.source,'version',r.version);
end; $$;

create or replace function public.save_workspace(p_pin text, p_source text, p_expected_version bigint)
returns jsonb language plpgsql security definer set search_path=public,extensions as $$
declare r public.workspaces;
begin
  update public.workspaces set source=coalesce(p_source,''),version=version+1,updated_at=now()
  where pin_hash=encode(digest(p_pin,'sha256'),'hex') and version=p_expected_version
  returning * into r;
  if not found then raise exception 'Conflitto: workspace modificato da un altro utente. Ricarica prima di salvare.'; end if;
  return jsonb_build_object('version',r.version,'updated_at',r.updated_at);
end; $$;

revoke all on function public.create_workspace(text,text,text) from public;
revoke all on function public.read_workspace(text) from public;
revoke all on function public.save_workspace(text,text,bigint) from public;
grant execute on function public.create_workspace(text,text,text) to anon, authenticated;
grant execute on function public.read_workspace(text) to anon, authenticated;
grant execute on function public.save_workspace(text,text,bigint) to anon, authenticated;
