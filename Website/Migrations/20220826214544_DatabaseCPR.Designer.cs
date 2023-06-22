﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Website.Data;

#nullable disable

namespace Website.Migrations
{
    [DbContext(typeof(WebsiteContext))]
    [Migration("20220826214544_DatabaseCPR")]
    partial class DatabaseCPR
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.8")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("Website.Data.User", b =>
                {
                    b.Property<int>("Vid")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<DateTime>("LastControlTime")
                        .HasColumnType("datetime");

                    b.Property<DateTime>("LastPilotTime")
                        .HasColumnType("datetime");

                    b.Property<ulong>("Roles")
                        .HasColumnType("bigint unsigned");

                    b.Property<ulong?>("Snowflake")
                        .HasColumnType("bigint unsigned");

                    b.HasKey("Vid");

                    b.ToTable("Users");
                });
#pragma warning restore 612, 618
        }
    }
}