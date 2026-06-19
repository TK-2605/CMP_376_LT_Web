using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<EmailOtp> EmailOtps { get; set; }

        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public DbSet<Subject> Subjects { get; set; }

        public DbSet<Class> Classes { get; set; }

        public DbSet<ClassMember> ClassMembers { get; set; }

        public DbSet<Question> Questions { get; set; }

        public DbSet<QuestionOption> QuestionOptions { get; set; }

        public DbSet<Exam> Exams { get; set; }

        public DbSet<ExamQuestion> ExamQuestions { get; set; }

        public DbSet<ExamAttempt> ExamAttempts { get; set; }

        public DbSet<AttemptAnswer> AttemptAnswers { get; set; }

        public DbSet<AntiCheatEvent> AntiCheatEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(user => user.FullName).HasMaxLength(150);
                entity.Property(user => user.StudentCode).HasMaxLength(50);
                entity.HasIndex(user => user.StudentCode).IsUnique().HasFilter("[StudentCode] IS NOT NULL");
            });

            builder.Entity<EmailOtp>(entity =>
            {
                entity.Property(otp => otp.Email).HasMaxLength(256);
                entity.Property(otp => otp.CodeHash).HasMaxLength(256);
                entity.HasIndex(otp => new { otp.UserId, otp.Purpose, otp.ExpiresAt });

                entity.HasOne(otp => otp.User)
                    .WithMany(user => user.EmailOtps)
                    .HasForeignKey(otp => otp.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<RefreshToken>(entity =>
            {
                entity.Property(token => token.TokenHash).HasMaxLength(256);
                entity.Property(token => token.IpAddress).HasMaxLength(64);
                entity.Property(token => token.UserAgent).HasMaxLength(512);
                entity.HasIndex(token => token.TokenHash).IsUnique();

                entity.HasOne(token => token.User)
                    .WithMany(user => user.RefreshTokens)
                    .HasForeignKey(token => token.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Subject>(entity =>
            {
                entity.Property(subject => subject.Code).HasMaxLength(50);
                entity.Property(subject => subject.Name).HasMaxLength(200);
                entity.HasIndex(subject => subject.Code).IsUnique();
            });

            builder.Entity<Class>(entity =>
            {
                entity.Property(classRoom => classRoom.Code).HasMaxLength(50);
                entity.Property(classRoom => classRoom.Name).HasMaxLength(200);
                entity.Property(classRoom => classRoom.Semester).HasMaxLength(50);
                entity.Property(classRoom => classRoom.AcademicYear).HasMaxLength(50);
                entity.HasIndex(classRoom => classRoom.Code).IsUnique();

                entity.HasOne(classRoom => classRoom.Subject)
                    .WithMany(subject => subject.Classes)
                    .HasForeignKey(classRoom => classRoom.SubjectId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(classRoom => classRoom.Teacher)
                    .WithMany(user => user.ClassesTeaching)
                    .HasForeignKey(classRoom => classRoom.TeacherId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ClassMember>(entity =>
            {
                entity.HasKey(member => new { member.ClassId, member.UserId });

                entity.HasOne(member => member.Class)
                    .WithMany(classRoom => classRoom.Members)
                    .HasForeignKey(member => member.ClassId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(member => member.User)
                    .WithMany(user => user.ClassMembers)
                    .HasForeignKey(member => member.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Question>(entity =>
            {
                entity.Property(question => question.Content).HasMaxLength(4000);
                entity.Property(question => question.Explanation).HasMaxLength(4000);

                entity.HasOne(question => question.Subject)
                    .WithMany(subject => subject.Questions)
                    .HasForeignKey(question => question.SubjectId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(question => question.CreatedBy)
                    .WithMany(user => user.QuestionsCreated)
                    .HasForeignKey(question => question.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<QuestionOption>(entity =>
            {
                entity.Property(option => option.Content).HasMaxLength(2000);

                entity.HasOne(option => option.Question)
                    .WithMany(question => question.Options)
                    .HasForeignKey(option => option.QuestionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Exam>(entity =>
            {
                entity.Property(exam => exam.Title).HasMaxLength(200);
                entity.Property(exam => exam.PassingScore).HasPrecision(6, 2);

                entity.HasOne(exam => exam.Subject)
                    .WithMany(subject => subject.Exams)
                    .HasForeignKey(exam => exam.SubjectId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(exam => exam.Class)
                    .WithMany(classRoom => classRoom.Exams)
                    .HasForeignKey(exam => exam.ClassId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(exam => exam.CreatedBy)
                    .WithMany(user => user.ExamsCreated)
                    .HasForeignKey(exam => exam.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ExamQuestion>(entity =>
            {
                entity.Property(examQuestion => examQuestion.Score).HasPrecision(6, 2);
                entity.HasIndex(examQuestion => new { examQuestion.ExamId, examQuestion.QuestionId }).IsUnique();

                entity.HasOne(examQuestion => examQuestion.Exam)
                    .WithMany(exam => exam.ExamQuestions)
                    .HasForeignKey(examQuestion => examQuestion.ExamId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(examQuestion => examQuestion.Question)
                    .WithMany(question => question.ExamQuestions)
                    .HasForeignKey(examQuestion => examQuestion.QuestionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ExamAttempt>(entity =>
            {
                entity.Property(attempt => attempt.Score).HasPrecision(6, 2);
                entity.Property(attempt => attempt.IpAddress).HasMaxLength(64);
                entity.Property(attempt => attempt.UserAgent).HasMaxLength(512);
                entity.Property(attempt => attempt.DeviceFingerprint).HasMaxLength(256);

                entity.HasOne(attempt => attempt.Exam)
                    .WithMany(exam => exam.Attempts)
                    .HasForeignKey(attempt => attempt.ExamId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(attempt => attempt.User)
                    .WithMany(user => user.ExamAttempts)
                    .HasForeignKey(attempt => attempt.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AttemptAnswer>(entity =>
            {
                entity.Property(answer => answer.AwardedScore).HasPrecision(6, 2);
                entity.HasIndex(answer => new { answer.ExamAttemptId, answer.QuestionId });

                entity.HasOne(answer => answer.ExamAttempt)
                    .WithMany(attempt => attempt.Answers)
                    .HasForeignKey(answer => answer.ExamAttemptId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(answer => answer.Question)
                    .WithMany(question => question.AttemptAnswers)
                    .HasForeignKey(answer => answer.QuestionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(answer => answer.SelectedOption)
                    .WithMany(option => option.AttemptAnswers)
                    .HasForeignKey(answer => answer.SelectedOptionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AntiCheatEvent>(entity =>
            {
                entity.Property(antiCheatEvent => antiCheatEvent.Description).HasMaxLength(1000);
                entity.Property(antiCheatEvent => antiCheatEvent.Note).HasMaxLength(1000);
                entity.Property(antiCheatEvent => antiCheatEvent.UserId).HasMaxLength(450);
                entity.HasIndex(antiCheatEvent => new { antiCheatEvent.ExamAttemptId, antiCheatEvent.CreatedAt });

                entity.HasOne(antiCheatEvent => antiCheatEvent.User)
                    .WithMany(user => user.AntiCheatEvents)
                    .HasForeignKey(antiCheatEvent => antiCheatEvent.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(antiCheatEvent => antiCheatEvent.Exam)
                    .WithMany()
                    .HasForeignKey(antiCheatEvent => antiCheatEvent.ExamId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(antiCheatEvent => antiCheatEvent.ExamAttempt)
                    .WithMany(attempt => attempt.AntiCheatEvents)
                    .HasForeignKey(antiCheatEvent => antiCheatEvent.ExamAttemptId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
