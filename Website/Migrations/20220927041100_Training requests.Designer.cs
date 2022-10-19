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
    [Migration("20220927041100_Training requests")]
    partial class Trainingrequests
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.9")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("Website.Data.DeviationReport", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Body")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Callsign")
                        .HasColumnType("longtext");

                    b.Property<DateTime>("FilingTime")
                        .HasColumnType("datetime");

                    b.Property<int>("Reportee")
                        .HasColumnType("int");

                    b.Property<int>("Reporter")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("Deviations");
                });

            modelBuilder.Entity("Website.Data.Document", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Departments")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Path")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Positions")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("Documents");
                });

            modelBuilder.Entity("Website.Data.Event", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Controllers")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<DateTime>("End")
                        .HasColumnType("datetime");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Positions")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Route")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<DateTime>("Start")
                        .HasColumnType("datetime");

                    b.HasKey("Id");

                    b.ToTable("Events");
                });

            modelBuilder.Entity("Website.Data.Exam", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<bool>("Mock")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("Position")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("Rating")
                        .HasColumnType("int");

                    b.Property<DateTime>("Start")
                        .HasColumnType("datetime");

                    b.Property<int>("Trainee")
                        .HasColumnType("int");

                    b.Property<int>("Trainer")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("Exams");
                });

            modelBuilder.Entity("Website.Data.TrainingRequest", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Comments")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Position")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("Rating")
                        .HasColumnType("int");

                    b.Property<int>("Trainee")
                        .HasColumnType("int");

                    b.Property<int?>("Trainer")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("TrainingRequests");
                });

            modelBuilder.Entity("Website.Data.User", b =>
                {
                    b.Property<int>("Vid")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Country")
                        .HasColumnType("longtext");

                    b.Property<string>("Division")
                        .HasColumnType("longtext");

                    b.Property<bool>("FaaChecked")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("FirstName")
                        .HasColumnType("longtext");

                    b.Property<DateTime>("LastControlTime")
                        .HasColumnType("datetime");

                    b.Property<string>("LastName")
                        .HasColumnType("longtext");

                    b.Property<DateTime>("LastPilotTime")
                        .HasColumnType("datetime");

                    b.Property<bool>("NavCanChecked")
                        .HasColumnType("tinyint(1)");

                    b.Property<int?>("RatingAtc")
                        .HasColumnType("int");

                    b.Property<int?>("RatingPilot")
                        .HasColumnType("int");

                    b.Property<ulong>("Roles")
                        .HasColumnType("bigint unsigned");

                    b.Property<ulong?>("Snowflake")
                        .HasColumnType("bigint unsigned");

                    b.Property<string>("Staff")
                        .HasColumnType("longtext");

                    b.HasKey("Vid");

                    b.ToTable("Users");
                });
#pragma warning restore 612, 618
        }
    }
}
